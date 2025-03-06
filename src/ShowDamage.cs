using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Utils;
using CS2_GameHUDAPI;

namespace CS2_ShowDamage
{
    public class ShowDamage : BasePlugin
	{
		static readonly byte HUDCHANNEL_DAMAGE = 15;
		static readonly byte HUDCHANNEL_SELFDAMAGE = 16;

		static readonly Random rd = new();
		static readonly Vector g_vecSelfDamage = new(0, -0.7f, 7);
		static Vector[] g_vecPlayer = new Vector[65];
		static IGameHUDAPI? _api;
		public override string ModuleName => "Show Damage";
		public override string ModuleDescription => "Shows the damage dealt to the player";
		public override string ModuleAuthor => "DarkerZ [RUS]";
		public override string ModuleVersion => "1.DZ.0";
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
			for (int i = 0; i < 65; i++) g_vecPlayer[i] = new(0, 0, 7);
			RegisterEventHandler<EventPlayerHurt>(OnEventPlayerHurt, HookMode.Post);
			RegisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull, HookMode.Post);
			RegisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect, HookMode.Pre);
		}
		public override void Unload(bool hotReload)
		{
			DeregisterEventHandler<EventPlayerHurt>(OnEventPlayerHurt, HookMode.Post);
			DeregisterEventHandler<EventPlayerConnectFull>(OnEventPlayerConnectFull, HookMode.Post);
			DeregisterEventHandler<EventPlayerDisconnect>(OnEventPlayerDisconnect, HookMode.Pre);
		}
		private HookResult OnEventPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
		{
			if (_api == null) return HookResult.Continue;
			CCSPlayerController? player = @event.Userid;
			CCSPlayerController? attacker = @event.Attacker;
			int iDamage = @event.DmgHealth;
			if (player != null && player.IsValid)
			{
				_api.Native_GameHUD_Show(player, HUDCHANNEL_SELFDAMAGE, $"-{iDamage}", 2.0f);
			}
			if (attacker != null && attacker.IsValid)
			{
				double r = rd.NextDouble() * 2 * Math.PI;
				g_vecPlayer[attacker.Slot].X = (float)Math.Cos(r) * 0.7f - 0.1f;
				g_vecPlayer[attacker.Slot].Y = (float)Math.Sin(r) * 0.7f + 0.1f;
				_api.Native_GameHUD_UpdateParams(attacker, HUDCHANNEL_DAMAGE, g_vecPlayer[attacker.Slot], System.Drawing.Color.Aqua, 32, "Verdana", 0.007f);
				_api.Native_GameHUD_Show(attacker, HUDCHANNEL_DAMAGE, $"{iDamage}", 2.0f);
			}
			return HookResult.Continue;
		}
		private HookResult OnEventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
		{
			if (_api == null) return HookResult.Continue;
			CCSPlayerController? player = @event.Userid;
			if (player != null && player.IsValid)
			{
				_api.Native_GameHUD_Remove(player, HUDCHANNEL_DAMAGE);
				_api.Native_GameHUD_Remove(player, HUDCHANNEL_SELFDAMAGE);
			}
			return HookResult.Continue;
		}

		private HookResult OnEventPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
		{
			if (_api == null) return HookResult.Continue;
			CCSPlayerController? player = @event.Userid;
			if (player != null && player.IsValid)
			{
				_api.Native_GameHUD_SetParams(player, HUDCHANNEL_DAMAGE, g_vecPlayer[player.Slot], System.Drawing.Color.Aqua, 32, "Verdana", 0.007f, PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER);
				_api.Native_GameHUD_SetParams(player, HUDCHANNEL_SELFDAMAGE, g_vecSelfDamage, System.Drawing.Color.Red, 32, "Verdana", 0.007f, PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER);
			}
			return HookResult.Continue;
		}
	}
}
