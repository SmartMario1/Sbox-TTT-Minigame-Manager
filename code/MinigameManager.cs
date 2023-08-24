using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Internal;
using TerrorTown;
using SM1Minigames.UI;
using System.Text.Json;

namespace SM1Minigames
{
	/// <summary>
	/// Minigames should be of this class. Please ensure all functionality of the minigame is only used if Active is true.
	/// </summary>
	public abstract class Minigame
	{

		/// <summary>
		/// Whether the minigame is currently active. All functions of the minigame must ONLY run if the minigame is active.
		/// </summary>
		public bool IsActive { get; set; } = false;

		/// <summary>
		/// The name of your Minigame. Please ensure that the name is unique, otherwise everything will break. Keep the name short and simple.
		/// </summary>
		public abstract string Name { get; set; }

		/// <summary>
		/// The descripion of your Minigame. This can be a longer description of what it does.
		/// </summary>
		public abstract string Description { get; set; }

		public override bool Equals( object obj )
		{
			var item = obj as Minigame;
			if ( item == null )
			{
				return false;
			}
			return (Name.ToLower() == item.Name.ToLower());
		}

		public override int GetHashCode()
		{
			return Name.ToLower().GetHashCode();
		}

		protected Minigame()
		{
			MinigameManager.RegisterMinigame( this );
		}

		/// <summary>
		/// This function is called at the start of a ttt round.
		/// </summary>
		public virtual void RoundStart()
		{
			if ( !IsActive ) { return; }
			Event.Register(this );
		}

		/// <summary>
		/// This function is called at the end of a ttt round.
		/// </summary>
		public virtual void RoundEnd()
		{
			if ( !IsActive ) { return; }
			Event.Unregister(this );
		}

		/// <summary>
		/// Used by the minigame manager. Do not call this.
		/// </summary>
		public void ToggleActive()
		{
			IsActive = !IsActive;
		}
	}

	/// <summary>
	/// This class implements all logic partaining to minigames.
	/// </summary>
	public static partial class MinigameManager
	{
		public static IList<Minigame> RegisteredMinigames { get; private set; } = new List<Minigame>();

		public static Dictionary<string, float> MinigameChances { get; private set; } = new Dictionary<string, float>();

		public static IList<Minigame> EnabledMinigames { get { return RegisteredMinigames.Where( x => MinigameChances[x.Name] >= 0 ).ToList(); } }

		private static Minigame ActiveMinigame { get; set; } = null;

		[ConVar.Replicated( "minigame_chance_per_round" )]
		public static float MinigameChance { get; set; }

		[ConVar.Replicated("minigame_final_round")]
		public static bool AlwaysFinalRound { get; set; }

		private static Minigame SelectRandomMinigame()
		{
			float totalFrequency = EnabledMinigames.Sum( x => MinigameChances[x.Name] );
			float selection = Game.Random.Float( totalFrequency );
			float frequencyTrack = 0;
			foreach ( Minigame game in EnabledMinigames )
			{
				frequencyTrack = frequencyTrack + MinigameChances[game.Name];
				if ( frequencyTrack > selection )
				{
					return game;
				}
			}
			throw new Exception( "How did you do this?  --- Random select not functioning" );
		}
		public static void RegisterMinigame( Minigame game )
		{
			RegisteredMinigames.Add( game );
			// This part of the code loads the previous config if it exists.
			if ( Game.IsServer )
			{
				if ( FileSystem.Data.FileExists( "minigame_config.json" ) )
				{
					try
					{
						var config = FileSystem.Data.ReadJson<Dictionary<string, float>>( "minigame_config.json" );
						if ( !config.ContainsKey( game.Name ) ) { MinigameChances.Add( game.Name, -1f ); return; }
						MinigameChances.Add( game.Name, config[game.Name] );
					}
					catch (JsonException) // Convert old configs to the new format
					{
						var config = FileSystem.Data.ReadJson<Dictionary<string, bool>>( "minigame_config.json" );
						if ( !config.ContainsKey( game.Name ) ) { MinigameChances.Add( game.Name, -1f ); return; }
						MinigameChances.Add( game.Name, (config[game.Name] ? 1f : -1f) );
					}
				}
				else
				{
					MinigameChances.Add( game.Name, 1f );
				}
			}
		}

		[ConCmd.Server( "minigame_get_all_minigames" )]
		public static void GetAllMinigame()
		{
			Log.Info( "Minigames:" );
			foreach ( var game in RegisteredMinigames )
			{
				Log.Info( " " + game.Name + "; " + MinigameChances[game.Name] );
			}
		}

		[ConCmd.Server( "minigame_get_enabled_minigames" )]
		public static void GetEnabledMinigame()
		{
			Log.Info( "Minigames:" );
			foreach ( var game in EnabledMinigames )
			{
				Log.Info( " " + game.Name + "; " + MinigameChances[game.Name] );
			}
		}

		[ConCmd.Server( "minigame_set_chance" )]
		public static void SetMinigameChance( float chance)
		{
			if ( !ValidateUser( ConsoleSystem.Caller.Pawn as TerrorTown.Player ) ) { Log.Error( "Insufficient permissions" ); return; }

			float realchance = Math.Clamp(chance, 0.0f, 1.0f );
			MinigameChance = realchance;
		}

		[ConCmd.Server( "minigame_always_final_round" )]
		public static void SetAlwaysFinalRound( bool option )
		{
			if ( !ValidateUser( ConsoleSystem.Caller.Pawn as TerrorTown.Player ) ) { Log.Error( "Insufficient permissions" ); return; }

			AlwaysFinalRound = option;
		}

		// To check permissions of moderator commands.
		private static bool ValidateUser(TerrorTown.Player ply)
		{
			Game.AssertServer();
			if ( ply.UserData.PermissionLevel == PermissionLevel.Moderator ||
				ply.UserData.PermissionLevel == PermissionLevel.Admin ||
				ply.UserData.PermissionLevel == PermissionLevel.SuperAdmin ||
				ply.Client.SteamId == Game.SteamId)
			{
				return true;
			}
			return false;
		}

		[ConCmd.Server( "minigame_set_minigame_chance" )]
		public static void SetIndividualMinigameChance( string name, float chance )
		{
			if ( !ValidateUser( ConsoleSystem.Caller.Pawn as TerrorTown.Player ) ) { Log.Error( "Insufficient permissions" ); return; }
			var game = RegisteredMinigames.FirstOrDefault( x => x.Name.ToLower() == name.ToLower() );
			if ( game == null )
			{
				Log.Info( "Couldn't find game " + name );
				return;
			}

			MinigameChances[name] = Math.Clamp(chance, -1, 1);
		}

		[Event( "Game.Round.Start" )]
		public static void OnRoundStart()
		{
			var game_chance = Math.Clamp( MinigameChance, 0f, 1f );
			if ( Game.Random.Float() <= game_chance || (AlwaysFinalRound && MyGame.Current.RoundNumber == MyGame.RoundCount) )
			{
				ActiveMinigame = SelectRandomMinigame();
				Log.Info( "Minigame time! Initialising " + ActiveMinigame.Name );
				ActiveMinigame.ToggleActive();
				Event.Run( "minigame_announcement", ActiveMinigame.Name );
				ActiveMinigame.RoundStart();
			}
		}

		[Event( "Game.Round.End" )]
		public static void OnRoundEnd()
		{
			if ( ActiveMinigame != null )
			{
				Log.Info( "Disabling active minigame" );
				ActiveMinigame.RoundEnd();
				ActiveMinigame.ToggleActive();
				ActiveMinigame = null;
			}
		}

		// This was used during dev for a lot of things.
		//[ConCmd.Client( "minigame_testing" )]
		//public static void test()
		//{
		//	int i = 0;
		//	int one = 0;
		//	while(i++ < 1000 ) 
		//	{
		//		Minigame game = SelectRandomMinigame();
		//		if (game.Name == "I AM SPEED")
		//		{
		//			one++;
		//		}
		//	}
		//	Log.Info( "I am speed: " + one + "; Other: " + (1000 - one) );
		//}

		[ConCmd.Server("minigame_save_config")]
		public static void SaveConfig()
		{
			if ( !ValidateUser( ConsoleSystem.Caller.Pawn as TerrorTown.Player ) ) { Log.Error( "Insufficient permissions" ); return; }
			FileSystem.Data.WriteAllText( "minigame_config.json", Json.Serialize(MinigameChances));
			FileSystem.Data.WriteAllText( "minigame_chance.txt", MinigameChance.ToString() );
			FileSystem.Data.WriteAllText( "minigame_final_round.txt", AlwaysFinalRound.ToString() );
			Event.Run( "minigame_full_sync", Json.Serialize(MinigameChances), MinigameChance, AlwaysFinalRound );
		}

		[Event("minigame_announcement")]
		[ClientRpc]
		public static void AnnounceMinigame(string gamename)
		{
			var game = RegisteredMinigames.FirstOrDefault( x => x.Name.ToLower() == gamename.ToLower() );
			if (game == null )
			{
				Log.Error( "Server chose a minigame that we don't have!" );
				return;
			}
			var announcement = Game.RootPanel.AddChild<MinigameAnnouncement>();
			announcement.SetGame(game);
		}

		[ConCmd.Client( "minigame_toggle_ui")]
		public static void ToggleUI()
		{
			var myPanel = Game.RootPanel.ChildrenOfType<MinigameSelectorUI>().FirstOrDefault();
			if (myPanel != null)
			{
				myPanel.Delete();
				return;
			}
			myPanel = Game.RootPanel.AddChild<MinigameSelectorUI>();
			myPanel.SetLocalGameList( MinigameChances, RegisteredMinigames );
		}

		[Event( "minigame_full_sync" )]
		[ClientRpc]
		public static void FullClientSync(string MGJson, float chance, bool final_round)
		{
			var minigdict = Json.Deserialize<Dictionary<string, float>>( MGJson );
			MinigameChances = minigdict;
			RegisteredMinigames = RegisteredMinigames.Where(x => minigdict.Keys.Contains(x.Name)).ToList();
			MinigameChance = chance;
			AlwaysFinalRound = final_round;
		}

		// This function triggers a manual sync in case something weird is happening.
		[ConCmd.Server("minigame_manual_sync")]
		public static void ManualSync()
		{
			if ( !ValidateUser( ConsoleSystem.Caller.Pawn as TerrorTown.Player ) ) { Log.Error( "Insufficient permissions" ); return; }
			Game.AssertServer();

			Event.Run( "minigame_full_sync", Json.Serialize(MinigameChances), MinigameChance, AlwaysFinalRound );
		}

		[GameEvent.Server.ClientJoined]
		public static void OnJoin(ClientJoinedEvent _e)
		{
			Event.Run( "minigame_full_sync", Json.Serialize(MinigameChances), MinigameChance, AlwaysFinalRound );
		}

		// These functions implement the chat commands !minigame and !minigames
		[TerrorTown.ChatCmd( "minigame", PermissionLevel.Moderator )]
		public static void ToggleUiMinigame()
		{
			ConsoleSystem.Caller.SendCommandToClient( "minigame_toggle_ui" );
		}

		[TerrorTown.ChatCmd( "minigames", PermissionLevel.Moderator )]
		public static void ToggleUiMinigames()
		{
			ConsoleSystem.Caller.SendCommandToClient( "minigame_toggle_ui" );
		}

		// Instantiation code (mostly) copied from Teams class in TTT by Three Thieves
		[Event( "Game.Initialized" )]
		public static void InitialiseMinigames( MyGame _game )
		{
			List<TypeDescription> list = (from type in GlobalGameNamespace.TypeLibrary.GetTypes<Minigame>()
										  where type.TargetType.IsSubclassOf( typeof( Minigame ) )
										  select type).ToList();
			if ( list.Count() == 0 )
			{
				Log.Error( "No minigame classes were found." );
				throw new Exception( "No minigame classes were found." );
			}

			foreach ( TypeDescription mg in list )
			{
				if ( !RegisteredMinigames.Where( ( Minigame x ) => x.GetType() == mg.TargetType ).Any() )
				{
					mg.Create<Minigame>();
				}
			}

			if ( Game.IsServer )
			{
				if ( FileSystem.Data.FileExists( "minigame_chance.txt" ) )
				{
					float chance = float.Parse( FileSystem.Data.ReadAllText( "minigame_chance.txt" ) );
					MinigameChance = chance;
				}
				else
				{
					MinigameChance = 0.25f;
				}

				if ( FileSystem.Data.FileExists( "minigame_final_round.txt" ) )
				{
					AlwaysFinalRound = bool.Parse( FileSystem.Data.ReadAllText( "minigame_final_round.txt" ) );
				}
				else
				{
					AlwaysFinalRound = false;
				}
				Event.Run( "minigame_full_sync", Json.Serialize(MinigameChances), MinigameChance, AlwaysFinalRound );
			}
		}
	}
}
