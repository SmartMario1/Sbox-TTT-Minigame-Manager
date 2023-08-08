﻿using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Internal;
using TerrorTown;
using SM1Minigames.UI;

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
		public static List<Minigame> RegisteredMinigames { get; private set; } = new List<Minigame>();

		public static List<Minigame> EnabledMinigames { get; private set; } = new List<Minigame>();

		private static Minigame ActiveMinigame { get; set; } = null;

		private static MinigameSelectorUI MyPanel { get; set; } = null;

		[ConVar.Replicated( "minigame_chance_per_round" )]
		public static float MinigameChance { get; set; }

		public static void RegisterMinigame( Minigame game )
		{
			RegisteredMinigames.Add( game );
			// This part of the code loads the previous config if it exists.
			if ( FileSystem.Data.FileExists( "minigame_config.json" ) )
			{
				var config = FileSystem.Data.ReadJson<Dictionary<string, bool>>( "minigame_config.json" );
				if ( !config.ContainsKey(game.Name) )
				{
					EnabledMinigames.Add( game );
					return;
				}
				if ( config[game.Name] ) 
				{
					EnabledMinigames.Add( game );
					return;
				}
			}
			else
			{
				EnabledMinigames.Add( game );
			}
		}

		[ConCmd.Server( "minigame_get_all_minigames" )]
		public static void GetAllMinigame()
		{
			Log.Info( "Minigames:" );
			foreach ( var game in RegisteredMinigames )
			{
				Log.Info( " " + game.Name );
			}
		}

		[ConCmd.Server( "minigame_get_enabled_minigames" )]
		public static void GetEnabledMinigame()
		{
			Log.Info( "Minigames:" );
			foreach ( var game in EnabledMinigames )
			{
				Log.Info( " " + game.Name );
			}
		}

		[ConCmd.Server( "minigame_set_chance" )]
		public static void SetMinigameChance( float chance)
		{
			if ( !ValidateUser( ConsoleSystem.Caller.Pawn as TerrorTown.Player ) ) { Log.Error( "Insufficient permissions" ); return; }

			float realchance = Math.Clamp(chance, 0.0f, 1.0f );
			FileSystem.Data.WriteAllText( "minigame_chance.txt", realchance.ToString() );
			MinigameChance = realchance;
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

		[ConCmd.Server( "minigame_enable_minigame" )]
		public static void SetMinigameEnabled( string name )
		{
			if ( !ValidateUser( ConsoleSystem.Caller.Pawn as TerrorTown.Player ) ) { Log.Error( "Insufficient permissions" ); return; }
			var game = RegisteredMinigames.Find( x => x.Name.ToLower() == name.ToLower() );
			if ( game == null )
			{
				Log.Info( "Couldn't find game " + name );
				return;
			}

			var m_game = EnabledMinigames.Find( x => x.Name.ToLower() == name.ToLower() );
			if ( m_game != null )
			{
				Log.Info( "Minigame was already enabled" );
				return;
			}
			EnabledMinigames.Add( game );
			SetMinigameEnabledClient( name );
		}

		[ConCmd.Server( "minigame_disable_minigame" )]
		public static void SetMinigameDisabled( string name )
		{
			if ( !ValidateUser( ConsoleSystem.Caller.Pawn as TerrorTown.Player ) ) { Log.Error( "Insufficient permissions" ); return; }
			var index = EnabledMinigames.FindIndex( x => x.Name.ToLower() == name.ToLower() );
			if ( index == -1 )
			{
				Log.Info( "Couldn't find enabled game " + name );
				return;
			}
			EnabledMinigames.RemoveAt( index );
			SetMinigameDisbledClient( name );
		}

		// These two functions try to ensure that the minigame list is synced between server and client.
		[ClientRpc]
		public static void SetMinigameEnabledClient( string name )
		{
			var game = RegisteredMinigames.Find( x => x.Name.ToLower() == name.ToLower() );
			EnabledMinigames.Add( game );
		}
		[ClientRpc]
		public static void SetMinigameDisbledClient( string name )
		{
			var index = EnabledMinigames.FindIndex( x => x.Name.ToLower() == name.ToLower() );
			EnabledMinigames.RemoveAt( index );
		}


		[Event( "Game.Round.Start" )]
		public static void OnRoundStart()
		{
			var game_chance = Math.Clamp( MinigameChance, 0f, 1f );
			if ( Game.Random.Float() <= game_chance )
			{
				var chosen_minigame = Game.Random.Int(EnabledMinigames.Count - 1);
				Log.Info( "Minigame time! Initialising " + EnabledMinigames[chosen_minigame].Name );
				ActiveMinigame = EnabledMinigames[chosen_minigame];
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
		//	Log.Info( "chance" + MinigameChance );
		//}

		[ConCmd.Server("minigame_save_config")]
		public static void SaveConfig()
		{
			if ( !ValidateUser( ConsoleSystem.Caller.Pawn as TerrorTown.Player ) ) { Log.Error( "Insufficient permissions" ); return; }
			FileSystem.Data.WriteAllText( "minigame_config.json", SM1Utils.Lists2Json( RegisteredMinigames, EnabledMinigames ));
		}

		[Event("minigame_announcement")]
		[ClientRpc]
		public static void AnnounceMinigame(string gamename)
		{
			var game = RegisteredMinigames.Find( x => x.Name.ToLower() == gamename.ToLower() );
			if (game == null )
			{
				Log.Error( "Server chose a minigame that we don't have!" );
				return;
			}
			var announcement = Game.RootPanel.FindRootPanel().AddChild<MinigameAnnouncement>();
			announcement.SetGame(game);
		}

		[ConCmd.Client( "minigame_toggle_ui")]
		public static void ToggleUI()
		{
			if (Game.IsServer)
			{
				Log.Info( "caller: " +  ConsoleSystem.Caller );
			}
			if ( MyPanel == null )
			{
				MyPanel = Game.RootPanel.FindRootPanel().AddChild<MinigameSelectorUI>();
				MyPanel.SetLocalGameList( EnabledMinigames, RegisteredMinigames );
			}
			else
			{
				MyPanel?.Delete();
				MyPanel = null;
			}
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

			if (FileSystem.Data.FileExists( "minigame_chance.txt" ) )
			{
				float chance = float.Parse(FileSystem.Data.ReadAllText( "minigame_chance.txt" ));
				MinigameChance = chance;
			}
			else
			{
				MinigameChance = 0.25f;
			}
		}
	}
}