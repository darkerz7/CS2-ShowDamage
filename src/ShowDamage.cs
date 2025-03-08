using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CS2_GameHUDAPI;
using System.Text.Json.Serialization;
#if (USE_CLIENTPREFS)
using ClientPrefsAPI;
#endif

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
		[JsonPropertyName("Color")] public int[] HUDCOLOR { get; set; } = [0, 255, 255, 255];
		[JsonPropertyName("Color_Self")] public int[] HUDSELFCOLOR { get; set; } = [255, 0, 0, 255];
	}
	public class ShowDamage : BasePlugin, IPluginConfig<HUDConfig>
	{
		public HUDConfig Config { get; set; } = new HUDConfig();
		static readonly Random rd = new();
		static Vector g_vecSelfDamage = new(0, -0.7f, 7);
		static Vector[] g_vecPlayer = new Vector[65];
		static bool[] g_bShow = new bool[65];
		static System.Drawing.Color g_colorDamage = System.Drawing.Color.Aqua;
		static System.Drawing.Color g_colorSelfDamage = System.Drawing.Color.Red;
		static IGameHUDAPI? _api;
#if (USE_CLIENTPREFS)
		static IClientPrefsAPI? _CP_api;
#endif
		public override string ModuleName => "Show Damage";
		public override string ModuleDescription => "Shows the damage dealt to the player";
		public override string ModuleAuthor => "DarkerZ [RUS]";
		public override string ModuleVersion => "1.DZ.6";
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

			if (config.HUDTIME_DAMAGE < 0.0f || config.HUDTIME_DAMAGE > 10.0f)
				config.HUDTIME_DAMAGE = 2.0f;

			if (config.HUDTIME_SELFDAMAGE < 0.0f || config.HUDTIME_SELFDAMAGE > 10.0f)
				config.HUDTIME_SELFDAMAGE = 2.0f;

			if (config.HUDRADIUS < 0.1f || config.HUDRADIUS > 10.0f)
				config.HUDRADIUS = 0.7f;

			if (config.HUDSELFX < -10.0f || config.HUDSELFX > 10.0f)
				config.HUDSELFX = 0.0f;

			if (config.HUDSELFY < -10.0f || config.HUDSELFY > 10.0f)
				config.HUDSELFY = -0.7f;

			if (config.HUDCOLOR.Length < 4)
				config.HUDCOLOR = [0, 255, 255, 255];
			for (int i = 0; i < config.HUDCOLOR.Length; i++)
				if (config.HUDCOLOR[i] < 0 || config.HUDCOLOR[i] > 255)
					config.HUDCOLOR[i] = 255;

			if (config.HUDSELFCOLOR.Length < 4)
				config.HUDSELFCOLOR = [0, 255, 255, 255];
			for (int i = 0; i < config.HUDSELFCOLOR.Length; i++)
				if (config.HUDSELFCOLOR[i] < 0 || config.HUDSELFCOLOR[i] > 255)
					config.HUDSELFCOLOR[i] = 255;

			Config = config;

			g_vecSelfDamage.X = Config.HUDSELFX;
			g_vecSelfDamage.Y = Config.HUDSELFY;
			g_colorDamage = System.Drawing.Color.FromArgb(Config.HUDCOLOR[3], Config.HUDCOLOR[0], Config.HUDCOLOR[1], Config.HUDCOLOR[2]);
			g_colorSelfDamage = System.Drawing.Color.FromArgb(Config.HUDSELFCOLOR[3], Config.HUDSELFCOLOR[0], Config.HUDSELFCOLOR[1], Config.HUDSELFCOLOR[2]);
		}
		public override void OnAllPluginsLoaded(bool hotReload)
		{
			try
			{
				PluginCapability<IGameHUDAPI> CapabilityGH = new("gamehud:api");
				_api = IGameHUDAPI.Capability.Get();
			}
			catch (Exception)
			{
				_api = null;
				Console.WriteLine($"[GameHUD] API Loading Failed!");
			}

#if (USE_CLIENTPREFS)
			try
			{
				PluginCapability<IClientPrefsAPI> CapabilityCP = new("clientprefs:api");
				_CP_api = IClientPrefsAPI.Capability.Get();
			}
			catch (Exception)
			{
				_CP_api = null;
				//Console.WriteLine($"[ClientPrefs] API Loading Failed!");
			}
#endif

			if (hotReload)
			{
				Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(player =>
				{
					SetHUD(player);
				});
			}
		}
		public override void Load(bool hotReload)
		{
			for (int i = 0; i < 65; i++)
			{
				g_bShow[i] = new();
				g_vecPlayer[i] = new(0, 0, 7);
			}
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
			if ((float)Config.HUDTIME_SELFDAMAGE > 0.0 && player != null && player.IsValid && g_bShow[player.Slot])
			{
				_api.Native_GameHUD_UpdateParams(player, Config.HUDCHANNEL_SELFDAMAGE, g_vecSelfDamage, g_colorSelfDamage, Config.HUDSIZE, Config.HUDFONT, Config.HUDUNITS);
				_api.Native_GameHUD_Show(player, Config.HUDCHANNEL_SELFDAMAGE, $"-{iDamage}", (float)Config.HUDTIME_SELFDAMAGE);

			}
			if ((float)Config.HUDTIME_DAMAGE > 0.0 && attacker != null && attacker.IsValid && g_bShow[attacker.Slot])
			{
				double r = rd.NextDouble() * 2 * Math.PI;
				g_vecPlayer[attacker.Slot].X = (float)Math.Cos(r) * Config.HUDRADIUS - 0.1f;
				g_vecPlayer[attacker.Slot].Y = (float)Math.Sin(r) * Config.HUDRADIUS + 0.1f;
				_api.Native_GameHUD_UpdateParams(attacker, Config.HUDCHANNEL_DAMAGE, g_vecPlayer[attacker.Slot], g_colorDamage, Config.HUDSIZE, Config.HUDFONT, Config.HUDUNITS);
				_api.Native_GameHUD_Show(attacker, Config.HUDCHANNEL_DAMAGE, $"{iDamage}", (float)Config.HUDTIME_DAMAGE);
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
				g_bShow[player.Slot] = string.IsNullOrEmpty(Config.HUDFLAG) || AdminManager.PlayerHasPermissions(player, Config.HUDFLAG);
#if (USE_CLIENTPREFS)
				if (_CP_api != null && g_bShow[player.Slot])
				{
					string sValue = _CP_api.GetClientCookie(player.SteamID.ToString(), "ShowDamage");
					if (!string.IsNullOrEmpty(sValue) && Int32.TryParse(sValue, out int iValue))
						if (iValue == 0) g_bShow[player.Slot] = false;
						else g_bShow[player.Slot] = true;
				}
#endif

				_api.Native_GameHUD_SetParams(player, Config.HUDCHANNEL_DAMAGE, g_vecPlayer[player.Slot], g_colorDamage, Config.HUDSIZE, Config.HUDFONT, Config.HUDUNITS, PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER);
				_api.Native_GameHUD_SetParams(player, Config.HUDCHANNEL_SELFDAMAGE, g_vecSelfDamage, g_colorSelfDamage, Config.HUDSIZE, Config.HUDFONT, Config.HUDUNITS, PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER);
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
		[ConsoleCommand("css_showdamage", "Toggle show damage")]
		[ConsoleCommand("css_showdmg", "Toggle show damage")]
		[ConsoleCommand("css_sd", "Toggle show damage")]
		[ConsoleCommand("css_damage", "Toggle show damage")]
		[ConsoleCommand("css_dmg", "Toggle show damage")]
		[CommandHelper(minArgs: 0, usage: "", whoCanExecute: CommandUsage.CLIENT_ONLY)]
		public void OnCommandSD(CCSPlayerController? player, CommandInfo command)
		{
			if (player == null || !player.IsValid) return;
			bool bAccess = string.IsNullOrEmpty(Config.HUDFLAG) || AdminManager.PlayerHasPermissions(player, Config.HUDFLAG);
			if (bAccess)
			{
				g_bShow[player.Slot] = !g_bShow[player.Slot];
				if (command.CallingContext == CommandCallingContext.Console) player.PrintToConsole($"[ShowDamage] is {(g_bShow[player.Slot] ? "Enabled" : "Disabled")}");
				else player.PrintToChat($" \x0B[\x04ShowDamage\x0B]\x01 is {(g_bShow[player.Slot] ? "Enabled" : "Disabled")}");
#if (USE_CLIENTPREFS)
				if (_CP_api != null)
				{
					if (g_bShow[player.Slot]) _CP_api.SetClientCookie(player.SteamID.ToString(), "ShowDamage", "1");
					else _CP_api.SetClientCookie(player.SteamID.ToString(), "ShowDamage", "0");
				}
#endif
			}
		}
	}
}
