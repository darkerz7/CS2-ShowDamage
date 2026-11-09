using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core.Attributes;
using static CounterStrikeSharp.API.Core.Listeners;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Extensions;

#if (USE_CLIENTPREFS)
using PlayerSettings;
#endif

namespace CS2_ShowDamage
{
	[MinimumApiVersion(379)]
	public class HUDConfig : BasePluginConfig
	{
		[JsonPropertyName("Time")] public float HUDTIME_DAMAGE { get; set; } = 2.0f;
		[JsonPropertyName("Time_Self")] public float HUDTIME_SELFDAMAGE { get; set; } = 2.0f;
		[JsonPropertyName("Flag")] public string? HUDFLAG { get; set; } = null;
	}

	class SD_UHUD
	{
		public bool Show;
		public double fTimeDamage;
		public double fTimeSelfDamage;
		public bool __Cache_Hide_Damage = true;
		public bool __Cache_Hide_Self_Damage = true;
		public int __Cache_Position_Damage;
		public SD_UHUD() { }
	}

    public class ShowDamage : BasePlugin, IPluginConfig<HUDConfig>
	{
		public HUDConfig Config { get; set; } = new HUDConfig();
        static CCSCustomHudLayout? g_CustomHudLayout;
        static double g_fGameTime;
        static CounterStrikeSharp.API.Modules.Timers.Timer? g_Timer = new(0.1f, GameTimeUpdate, TimerFlags.REPEAT);
        static readonly SD_UHUD[] g_Hud = new SD_UHUD[65];
#if (USE_CLIENTPREFS)
		private ISettingsApi? _PlayerSettingsAPI;
		private readonly PluginCapability<ISettingsApi?> _PlayerSettingsAPICapability = new("settings:nfcore");
#endif
		public override string ModuleName => "Show Damage";
		public override string ModuleDescription => "Shows the damage dealt to the player";
		public override string ModuleAuthor => "DarkerZ [RUS]";
		public override string ModuleVersion => "1.DZ.9";
		public void OnConfigParsed(HUDConfig config)
		{
			if (config.HUDTIME_DAMAGE < 0.0f || config.HUDTIME_DAMAGE > 10.0f)
				config.HUDTIME_DAMAGE = 2.0f;

			if (config.HUDTIME_SELFDAMAGE < 0.0f || config.HUDTIME_SELFDAMAGE > 10.0f)
				config.HUDTIME_SELFDAMAGE = 2.0f;

			Config = config;
		}
		public override void OnAllPluginsLoaded(bool hotReload)
		{

#if (USE_CLIENTPREFS)
			_PlayerSettingsAPI = _PlayerSettingsAPICapability.Get();
			if (_PlayerSettingsAPI == null)
				Console.WriteLine($"[PlayerSettings] API Loading Failed!");
#endif

			if (hotReload)
			{
				Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(player =>
				{
                    SetHUDSettings(player);
				});
			}
		}
		public override void Load(bool hotReload)
		{
			for (int i = 0; i < 65; i++) g_Hud[i] = new();
            RegisterEventHandler<EventPlayerHurt>(OnEventPlayerHurt, HookMode.Post);
			RegisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull, HookMode.Post);
			RegisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect, HookMode.Pre);
            RegisterListener<OnMapStart>(OnMapStart_Listener);
            RegisterListener<OnMapEnd>(OnMapEnd_Listener);
        }
		public override void Unload(bool hotReload)
		{
			DeregisterEventHandler<EventPlayerHurt>(OnEventPlayerHurt, HookMode.Post);
			DeregisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull, HookMode.Post);
			DeregisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect, HookMode.Pre);
            RemoveListener<OnMapStart>(OnMapStart_Listener);
            RemoveListener<OnMapEnd>(OnMapEnd_Listener);
            Utilities.GetPlayers().ForEach(player => { RemoveHUDSettings(player); });
			g_Timer?.Kill();
			g_Timer = null;
        }

		private void OnMapStart_Listener(string sMapName)
		{
			Server.NextFrame(() =>
			{
				GetorCreateHudLayout();
            });
        }

        private void OnMapEnd_Listener() => RemoveHudLayout();

		private static void GameTimeUpdate()
		{
			g_fGameTime = Server.EngineTime;
            Utilities.GetPlayers().Where(p => p is { IsValid: true, IsBot: false, IsHLTV: false }).ToList().ForEach(player =>
            {
				if (g_Hud[player.Slot].fTimeDamage < g_fGameTime) HideHudDamage(player);
                if (g_Hud[player.Slot].fTimeSelfDamage < g_fGameTime) HideHudSelfDamage(player);
            });
        }

        public static CCSCustomHudLayout? GetorCreateHudLayout()
		{
            if (g_CustomHudLayout is { IsValid: true }) return g_CustomHudLayout;
            if (Utilities.CreateEntityByName<CCSCustomHudLayout>("custom_hud_layout") is { IsValid: true } customhud)
            {
                customhud.StrLayout = "panorama/layout/custom_game/showdamage_dz.vxml_c";
                return g_CustomHudLayout = customhud;
            }
            return null;
        }
        public static void RemoveHudLayout() => g_CustomHudLayout = null;

        private HookResult OnEventPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
		{
			if (GetorCreateHudLayout() is { })
			{
				CCSPlayerController? player = @event.Userid;
				CCSPlayerController? attacker = @event.Attacker;
				int iDamage = @event.DmgHealth;
				if ((float)Config.HUDTIME_SELFDAMAGE > 0.0 && player != null && player.IsValid && g_Hud[player.Slot].Show)
				{
					g_Hud[player.Slot].fTimeSelfDamage = g_fGameTime + Config.HUDTIME_SELFDAMAGE;
					ShowHudSelfDamage(player, iDamage);
                }
				if ((float)Config.HUDTIME_DAMAGE > 0.0 && attacker != null && attacker.IsValid && g_Hud[attacker.Slot].Show)
				{
					g_Hud[attacker.Slot].fTimeDamage = g_fGameTime + Config.HUDTIME_DAMAGE;
                    ShowHudDamage(attacker, iDamage);
                }
			}
			return HookResult.Continue;
		}
		private HookResult OnEventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
		{
			CCSPlayerController? player = @event.Userid;
            RemoveHUDSettings(player);
			return HookResult.Continue;
		}

		private HookResult OnEventPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
		{
			CCSPlayerController? player = @event.Userid;
            SetHUDSettings(player);
			return HookResult.Continue;
		}

		static void ShowHudDamage(CCSPlayerController player, int iDamage)
		{
            if (GetorCreateHudLayout() is { } hud)
			{
                hud.SetDialogVariableStringForPlayer(player, "sd-damage", "sd-damage-text", $"{iDamage}");
				if (g_Hud[player.Slot].__Cache_Hide_Damage)
				{
					g_Hud[player.Slot].__Cache_Hide_Damage = false;
                    hud.SetHasClassForPlayer(player, "sd-damage", "sd-class-hide", false);
				}
				int r = Random.Shared.Next(0, 30);
				if (g_Hud[player.Slot].__Cache_Position_Damage != r)
				{
                    hud.SetHasClassForPlayer(player, "sd-damage", $"sd-class-position-{g_Hud[player.Slot].__Cache_Position_Damage}", false);
                    hud.SetHasClassForPlayer(player, "sd-damage", $"sd-class-position-{r}", true);
					g_Hud[player.Slot].__Cache_Position_Damage = r;
                }
            }
        }

        static void ShowHudSelfDamage(CCSPlayerController player, int iDamage)
		{
            if (GetorCreateHudLayout() is { } hud)
            {
                hud.SetDialogVariableStringForPlayer(player, "sd-self-damage", "sd-self-damage-text", $"-{iDamage}");
                if (g_Hud[player.Slot].__Cache_Hide_Self_Damage)
				{
                    g_Hud[player.Slot].__Cache_Hide_Self_Damage = false;
                    hud.SetHasClassForPlayer(player, "sd-self-damage", "sd-class-hide", false);
                }
            }
        }

		static void HideHudDamage(CCSPlayerController player)
		{
            if (GetorCreateHudLayout() is { } hud)
            {
                if (!g_Hud[player.Slot].__Cache_Hide_Damage)
				{
                    g_Hud[player.Slot].__Cache_Hide_Damage = true;
					hud.SetHasClassForPlayer(player, "sd-damage", "sd-class-hide", true);
                }
            }
        }

		static void HideHudSelfDamage(CCSPlayerController player)
		{
            if (GetorCreateHudLayout() is { } hud)
            {
                if (!g_Hud[player.Slot].__Cache_Hide_Self_Damage)
				{
                    g_Hud[player.Slot].__Cache_Hide_Self_Damage = true;
                    hud.SetHasClassForPlayer(player, "sd-self-damage", "sd-class-hide", true);
                }
            }
        }

        static void SetDefaultHudPosition(CCSPlayerController player)
		{
			if (GetorCreateHudLayout() is { } hud)
			{
				if (g_Hud[player.Slot].__Cache_Position_Damage != 0)
				{
					hud.SetHasClassForPlayer(player, "sd-damage", $"sd-class-position-{g_Hud[player.Slot].__Cache_Position_Damage}", false);
					hud.SetHasClassForPlayer(player, "sd-damage", "sd-class-position-0", true);
					g_Hud[player.Slot].__Cache_Position_Damage = 0;
				}
			}
        }

        void SetHUDSettings(CCSPlayerController? player)
		{
			if (player != null && player.IsValid)
			{
                g_Hud[player.Slot].Show = string.IsNullOrEmpty(Config.HUDFLAG) || AdminManager.PlayerHasPermissions(player, Config.HUDFLAG);
                g_Hud[player.Slot].fTimeDamage = g_fGameTime - Config.HUDTIME_DAMAGE - -1.0f;
                g_Hud[player.Slot].fTimeSelfDamage = g_fGameTime - Config.HUDTIME_SELFDAMAGE - -1.0f;
#if (USE_CLIENTPREFS)
                if (_PlayerSettingsAPI != null && g_Hud[player.Slot].Show)
				{
					string sValue = _PlayerSettingsAPI.GetPlayerSettingsValue(player, "ShowDamage", "1");
					if (!string.IsNullOrEmpty(sValue) && Int32.TryParse(sValue, out int iValue))
						if (iValue == 0) g_Hud[player.Slot].Show = false;
						else g_Hud[player.Slot].Show = true;
				}
#endif
			}
		}
		void RemoveHUDSettings(CCSPlayerController? player)
		{
			if (player != null && player.IsValid)
			{
                g_Hud[player.Slot].Show = false;
                g_Hud[player.Slot].fTimeDamage = g_fGameTime - Config.HUDTIME_DAMAGE - 1.0f;
                g_Hud[player.Slot].fTimeSelfDamage = g_fGameTime - Config.HUDTIME_SELFDAMAGE - 1.0f;
				HideHudDamage(player);
				HideHudSelfDamage(player);
				SetDefaultHudPosition(player);
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
                g_Hud[player.Slot].Show = !g_Hud[player.Slot].Show;
				if (command.CallingContext == CommandCallingContext.Console) player.PrintToConsole($"[ShowDamage] is {(g_Hud[player.Slot].Show ? "Enabled" : "Disabled")}");
				else player.PrintToChat($" \x0B[\x04ShowDamage\x0B]\x01 is {(g_Hud[player.Slot].Show ? "Enabled" : "Disabled")}");
#if (USE_CLIENTPREFS)
				if (_PlayerSettingsAPI != null)
				{
					if (g_Hud[player.Slot].Show) _PlayerSettingsAPI.SetPlayerSettingsValue(player, "ShowDamage", "1");
					else _PlayerSettingsAPI.SetPlayerSettingsValue(player, "ShowDamage", "0");
				}
#endif
			}
		}
	}
}
