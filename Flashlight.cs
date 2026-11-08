using System.Drawing;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;

namespace Flashlight;

public sealed class FlashlightConfig
{
    public bool EnabledByDefault { get; set; }
    public float Brightness { get; set; } = 5f;
    public float Range { get; set; } = 2000.0f;
    public string Color { get; set; } = "255 255 255";
    public float InnerCone { get; set; } = 20.0f;
    public float OuterCone { get; set; } = 50.0f;
    public float Exponent { get; set; } = 2.0f;
    public float ConstantAttenuation { get; set; } = 0.15f;
    public float LinearAttenuation { get; set; } = 0.000001f;
    public float QuadraticAttenuation { get; set; } = 0.0000001f;
    public float ForwardOffset { get; set; } = 30f;
    public float HeightOffset { get; set; } = 64.0f;
}

public sealed class FlashlightPlugin : BasePlugin
{
    private readonly Dictionary<ulong, CBaseEntity> _lights = new();
    private readonly HashSet<ulong> _enabled = new();
    private FlashlightConfig _config = new();
    private string ConfigPath => Path.Combine(ModuleDirectory, "flashlight.json");

    public override string ModuleName => "CS2 Flashlight";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "TheIriya";
    public override string ModuleDescription => "CS2 player flashlight";

    public override void Load(bool hotReload)
    {
        LoadConfig();
        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterListener<Listeners.OnPlayerButtonsChanged>(OnButtonsChanged);
        RegisterListener<Listeners.OnClientDisconnect>(OnDisconnect);
        AddCommand("css_flashlight", "Toggle flashlight (0/1)", ToggleCommand);
        AddCommand("css_flashlight_reload", "Reload flashlight configuration", ReloadCommand);

        if (_config.EnabledByDefault)
        {
            foreach (var player in Utilities.GetPlayers())
            {
                if (player.IsValid)
                    _enabled.Add(player.SteamID);
            }
        }
    }

    private void LoadConfig()
    {
        Directory.CreateDirectory(ModuleDirectory);
        if (!File.Exists(ConfigPath))
        {
            File.WriteAllText(ConfigPath, """
            {
              "EnabledByDefault": false,
              "Brightness": 5,
              "Range": 1500,
              "Color": "255 255 255",
              "InnerCone": 20,
              "OuterCone": 50,
              "Exponent": 2,
              "ConstantAttenuation": 0.15,
              "LinearAttenuation": 0.000001,
              "QuadraticAttenuation": 0.0000001,
              "ForwardOffset": 0,
              "HeightOffset": 64
            }
            """);
        }

        try
        {
            var json = string.Join(Environment.NewLine,
                File.ReadLines(ConfigPath).Where(line => !line.TrimStart().StartsWith("//")));
            _config = JsonSerializer.Deserialize<FlashlightConfig>(json) ?? new();
        }
        catch
        {
            _config = new();
        }
    }

    private void OnButtonsChanged(CCSPlayerController player, PlayerButtons pressed, PlayerButtons released)
    {
        const PlayerButtons lookAtWeapon = (PlayerButtons)(1UL << 35);
        if (!pressed.HasFlag(lookAtWeapon))
            return;

        AddTimer(0.01f, () =>
        {
            if (player.IsValid)
                TogglePlayer(player);
        });
    }

    private void OnTick()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || !player.PawnIsAlive || player.Pawn.Value is not { } pawn)
            {
                if (player.IsValid)
                    RemoveLight(player.SteamID);
                continue;
            }

            if (!_enabled.Contains(player.SteamID))
            {
                RemoveLight(player.SteamID);
                continue;
            }

            if (pawn.AbsOrigin is not { } origin)
                continue;
            var angles = pawn.V_angle;
            var pitch = angles.X * MathF.PI / 180.0f;
            var yaw = angles.Y * MathF.PI / 180.0f;
            var forward = new Vector(
                MathF.Cos(pitch) * MathF.Cos(yaw),
                MathF.Cos(pitch) * MathF.Sin(yaw),
                -MathF.Sin(pitch));
            var lightOrigin = new Vector(
                origin.X + forward.X * _config.ForwardOffset,
                origin.Y + forward.Y * _config.ForwardOffset,
                origin.Z + _config.HeightOffset + forward.Z * _config.ForwardOffset);

            var light = GetOrCreateLight(player.SteamID, lightOrigin, angles);
            if (light.IsValid)
                light.Teleport(lightOrigin, angles, null);
        }
    }

    private CBaseEntity GetOrCreateLight(ulong steamId, Vector origin, QAngle angles)
    {
        if (_lights.TryGetValue(steamId, out var existing) && existing.IsValid)
            return existing;

        var light = Utilities.CreateEntityByName<CBaseEntity>("light_dynamic");
        if (light is null || !light.IsValid)
            return light!;

        var color = ParseColor();
        var keyValues = new CEntityKeyValues();
        keyValues.SetString("_light", $"{color.R} {color.G} {color.B} 255");
        keyValues.SetInt("brightness", Math.Max(1, (int)Math.Round(_config.Brightness)));
        keyValues.SetFloat("distance", _config.Range);
        keyValues.SetFloat("_farz", _config.Range);
        keyValues.SetFloat("_nearz", 4.0f);
        keyValues.SetBool("spotlight", true);
        keyValues.SetFloat("spotlight_radius", 2.0f);
        keyValues.SetFloat("_cone", _config.OuterCone);
        keyValues.SetFloat("_inner_cone", _config.InnerCone);
        keyValues.SetFloat("_exponent", _config.Exponent);
        keyValues.SetFloat("style", 0.0f);

        try
        {
            light.DispatchSpawn(keyValues);
        }
        catch (InvalidOperationException)
        {
            return light;
        }

        if (!light.IsValid)
            return light;

        light.Teleport(origin, angles, null);
        _lights[steamId] = light;
        return light;
    }

    private Color ParseColor()
    {
        var parts = _config.Color.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || !byte.TryParse(parts[0], out var red) ||
            !byte.TryParse(parts[1], out var green) || !byte.TryParse(parts[2], out var blue))
            return Color.White;

        return Color.FromArgb(red, green, blue);
    }

    private void ToggleCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player is null || command.ArgCount < 2 ||
            command.GetArg(1) is not ("0" or "1") ||
            !AdminManager.PlayerHasPermissions(player, "@css/root"))
            return;

        var enabled = command.GetArg(1) == "1";
        foreach (var target in Utilities.GetPlayers())
        {
            if (enabled)
                _enabled.Add(target.SteamID);
            else
            {
                _enabled.Remove(target.SteamID);
                RemoveLight(target.SteamID);
            }
        }
    }

    private void TogglePlayer(CCSPlayerController player)
    {
        if (_enabled.Remove(player.SteamID))
            RemoveLight(player.SteamID);
        else
            _enabled.Add(player.SteamID);
    }

    private void ReloadCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player is null || !AdminManager.PlayerHasPermissions(player, "@css/root"))
            return;

        LoadConfig();
        foreach (var id in _lights.Keys.ToArray())
            RemoveLight(id);
    }

    private void OnDisconnect(int slot)
    {
        var player = Utilities.GetPlayerFromSlot(slot);
        if (player is null)
            return;

        _enabled.Remove(player.SteamID);
        RemoveLight(player.SteamID);
    }

    private void RemoveLight(ulong id)
    {
        if (_lights.Remove(id, out var light) && light.IsValid)
            light.AcceptInput("Kill", null, null, "", 0);
    }
}
