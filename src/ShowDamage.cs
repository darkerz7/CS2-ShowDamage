using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Utils;
using CS2_GameHUDAPI;
using System.Text.Json.Serialization;

namespace CS2_ShowDamage
{
	public class HUDConfig : BasePluginConfig
	{
		[JsonPropertyName("Channel_Damage")] public byte HUDCHANNEL_DAMAGE { get; set; } = 15;
		[JsonPropertyName("Channel_SelfDamage")] public byte HUDCHANNEL_SELFDAMAGE { get; set; } = 16;
		[JsonPropertyName("Size")] public int HUDSIZE { get; set; } = 32;
		[JsonPropertyName("Font")] public string HUDFONT { get; set; } = "Verdana";
		[JsonPropertyName("WorldUnitsPerPx")] public float HUDUNITS { get; set; } = 0.007f;
		[JsonPropertyName("Time")] public float HUDTIME_DAMAGE { get; set; } = 2.0f;
		[JsonPropertyName("Time_Self")] public float HUDTIME_SELFDAMAGE { get; set; } = 2.0f;
		[JsonPropertyName("Radius")] public float HUDRADIUS { get; set; } = 0.7f;
		[JsonPropertyName("Self_PosX")] public float HUDSELFX { get; set; } = 0.0f;
		[JsonPropertyName("Self_PosY")] public float HUDSELFY { get; set; } = -0.7f;
		[JsonPropertyName("Flag")] public string? HUDFLAG { get; set; } = null;
		[JsonPropertyName("Color")] public System.Drawing.Color HUDCOLOR { get; set; } = System.Drawing.Color.Aqua;
		[JsonPropertyName("Color_Self")] public System.Drawing.Color HUDSELFCOLOR { get; set; } = System.Drawing.Color.Red;
	}
	public class ShowDamage : BasePlugin, IPluginConfig<HUDConfig>
	{
		public HUDConfig Config { get; set; } = new HUDConfig();
		static readonly Random rd = new();
		static Vector g_vecSelfDamage = new(0, -0.7f, 7);
		static Vector[] g_vecPlayer = new Vector[65];
		static bool[] g_bShow = new bool[65];
		static IGameHUDAPI? _api;
		public override string ModuleName => "Show Damage";
		public override string ModuleDescription => "Shows the damage dealt to the player";
		public override string ModuleAuthor => "DarkerZ [RUS]";
		public override string ModuleVersion => "1.DZ.4";
		public void OnConfigParsed(HUDConfig config)
		{
			if (config.HUDCHANNEL_DAMAGE < 0 || config.HUDCHANNEL_DAMAGE > 32)
				config.HUDCHANNEL_DAMAGE = 15;

			if (config.HUDCHANNEL_SELFDAMAGE < 0 || config.HUDCHANNEL_SELFDAMAGE > 32)
				config.HUDCHANNEL_SELFDAMAGE = 15;

			if (config.HUDSIZE < 16 || config.HUDSIZE > 256)
				config.HUDSIZE = 32;

			if (string.IsNullOrEmpty(config.HUDFONT))
				config.HUDFONT = "Verdana";

			if (config.HUDTIME_DAMAGE < 0.1f || config.HUDTIME_DAMAGE > 10.0f)
				config.HUDTIME_DAMAGE = 2.0f;

			if (config.HUDTIME_SELFDAMAGE < 0.1f || config.HUDTIME_SELFDAMAGE > 10.0f)
				config.HUDTIME_SELFDAMAGE = 2.0f;

			if (config.HUDRADIUS < 0.1f || config.HUDRADIUS > 10.0f)
				config.HUDRADIUS = 0.7f;

			if (config.HUDSELFX < -10.0f || config.HUDSELFX > 10.0f)
				config.HUDSELFX = 0.0f;

			if (config.HUDSELFY < -10.0f || config.HUDSELFY > 10.0f)
				config.HUDSELFY = -0.7f;

			Config = config;

			g_vecSelfDamage.X = Config.HUDSELFX;
			g_vecSelfDamage.Y = Config.HUDSELFY;
		}
		public override void OnAllPluginsLoaded(bool hotReload)
		{
			try
			{
				PluginCapability<IGameHUDAPI> CapabilityCP = new("gamehud:api");
				_api = IGameHUDAPI.Capability.Get();
			}
			catch (Exception)
			{
				_api = null;
				Console.WriteLine($"[GameHUD] API Loading Failed!");
			}
		}
		public override void Load(bool hotReload)
		{
			for (int i = 0; i < 65; i++)
			{
				g_bShow[i] = new();
				g_vecPlayer[i] = new(0, 0, 7);
			}
			if (hotReload) Utilities.GetPlayers().ForEach(player => { SetHUD(player); });
			RegisterEventHandler<EventPlayerHurt>(OnEventPlayerHurt, HookMode.Post);
			RegisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull, HookMode.Post);
			RegisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect, HookMode.Pre);
		}
		public override void Unload(bool hotReload)
		{
			DeregisterEventHandler<EventPlayerHurt>(OnEventPlayerHurt, HookMode.Post);
			DeregisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull, HookMode.Post);
			DeregisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect, HookMode.Pre);
			Utilities.GetPlayers().ForEach(player => { RemoveHUD(player); });
		}
		private HookResult OnEventPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
		{
			if (_api == null) return HookResult.Continue;
			CCSPlayerController? player = @event.Userid;
			CCSPlayerController? attacker = @event.Attacker;
			int iDamage = @event.DmgHealth;
			if (player != null && player.IsValid && g_bShow[player.Slot])
			{
				_api.Native_GameHUD_UpdateParams(player, Config.HUDCHANNEL_SELFDAMAGE, g_vecSelfDamage, System.Drawing.Color.Red, Config.HUDSIZE, Config.HUDFONT, Config.HUDUNITS);
				_api.Native_GameHUD_Show(player, Config.HUDCHANNEL_SELFDAMAGE, $"-{iDamage}", Config.HUDTIME_SELFDAMAGE);
			}
			if (attacker != null && attacker.IsValid && g_bShow[attacker.Slot])
			{
				double r = rd.NextDouble() * 2 * Math.PI;
				g_vecPlayer[attacker.Slot].X = (float)Math.Cos(r) * Config.HUDRADIUS - 0.1f;
				g_vecPlayer[attacker.Slot].Y = (float)Math.Sin(r) * Config.HUDRADIUS + 0.1f;
				_api.Native_GameHUD_UpdateParams(attacker, Config.HUDCHANNEL_DAMAGE, g_vecPlayer[attacker.Slot], System.Drawing.Color.Aqua, Config.HUDSIZE, Config.HUDFONT, Config.HUDUNITS);
				_api.Native_GameHUD_Show(attacker, Config.HUDCHANNEL_DAMAGE, $"{iDamage}", Config.HUDTIME_DAMAGE);
			}
			return HookResult.Continue;
		}
		private HookResult OnEventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
		{
			CCSPlayerController? player = @event.Userid;
			RemoveHUD(player);
			return HookResult.Continue;
		}

		private HookResult OnEventPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
		{
			CCSPlayerController? player = @event.Userid;
			SetHUD(player);
			return HookResult.Continue;
		}

		void SetHUD(CCSPlayerController? player)
		{
			if (_api != null && player != null && player.IsValid)
			{
				if (string.IsNullOrEmpty(Config.HUDFLAG)) g_bShow[player.Slot] = true;
				else g_bShow[player.Slot] = AdminManager.PlayerHasPermissions(player, Config.HUDFLAG);
				_api.Native_GameHUD_SetParams(player, Config.HUDCHANNEL_DAMAGE, g_vecPlayer[player.Slot], System.Drawing.Color.Aqua, Config.HUDSIZE, Config.HUDFONT, Config.HUDUNITS, PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER);
				_api.Native_GameHUD_SetParams(player, Config.HUDCHANNEL_SELFDAMAGE, g_vecSelfDamage, System.Drawing.Color.Red, Config.HUDSIZE, Config.HUDFONT, Config.HUDUNITS, PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER);
			}
		}

		void RemoveHUD(CCSPlayerController? player)
		{
			if (_api != null && player != null && player.IsValid)
			{
				g_bShow[player.Slot] = false;
				_api.Native_GameHUD_Remove(player, Config.HUDCHANNEL_DAMAGE);
				_api.Native_GameHUD_Remove(player, Config.HUDCHANNEL_SELFDAMAGE);
			}
		}
	}
}
