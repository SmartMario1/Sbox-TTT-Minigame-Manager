using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using TerrorTown;
using SM1Minigames;

// This file contains the first minigame update minigames.

namespace SM1MinigamePack1
{

	public class EveryoneFart : Minigame
	{
		public override string Name { get; set; } = "Mean bean machine";

		public override string Description { get; set; } = "Every player has become gassy! Players will randomly fart, knocking other players away!";

		private RealTimeSince last_fart { get; set; }

		private Particles Particle;

		private float time_to_fart;

		public override void RoundStart()
		{
			base.RoundStart();
			last_fart = 0;
			// Divide by sqrt of connected clients to increase the amount of farts when there are a lot of players.
			time_to_fart = Game.Random.Float( 8, 24 ) / ((float)Math.Sqrt( Game.Clients.Count ));
		}

		[GameEvent.Tick.Server]
		public void FartingTicker()
		{
			if ( IsActive )
			{
				if ( last_fart >= time_to_fart )
				{
					var farter = Utils.getAlivePlayer();

					// Following code inspired by the implementation of the discombobulator in TTT by Three Thieves
					if ( Particle == null )
					{
						Particle = Particles.Create( "particles/discombob_bomb.vpcf" );
						Particle.SetPosition( 0, farter.Position );
						ParticleCleanupSystem.RegisterForCleanup( Particle );
					}

					farter.PlaySound( "mgfart" );
					foreach ( Entity item in Entity.FindInSphere( farter.Position, 200f ) )
					{
						if ( item == farter ) { continue; }
						Vector3 normal = (item.Position - farter.Position).Normal;
						normal.z = Math.Abs( normal.z ) + 1f;
						item.Velocity += normal * 256f;
						ModelEntity modelEntity = item as ModelEntity;
						if ( modelEntity != null )
						{
							modelEntity.PhysicsBody.Velocity += normal * 50f;
						}

					}

					last_fart = 0;
					time_to_fart = Game.Random.Float( 8, 24 ) / ((float) Math.Sqrt( Game.Clients.Count )) ;
					Particle = null;
				}
			}
		}
	}

	public partial class DemocracyGame : Minigame
	{
		public override string Name { get; set; } = "Democracy";

		public override string Description { get; set; } = "Vote on a player to kill after every 45 seconds!";

		private const int vote_time = 15;

		private static Dictionary<string, string> vote_list { get; set; }

		public RealTimeSince LastVote { get; set; }

		public RealTimeSince StartedVote { get; set; }

		private static bool handled_vote { get; set; } = true;
		public override void RoundStart()
		{
			base.RoundStart();
			LastVote = vote_time;
			handled_vote = true;
			StartedVote = 0;
			vote_list = new Dictionary<string, string>();
		}

		private static string get_most_voted( Dictionary<string, string> dict )
		{
			Dictionary<string, int> countDict = new Dictionary<string, int>();

			foreach ( var kvp in dict )
			{
				string value = kvp.Value;

				if ( countDict.ContainsKey( value ) )
				{
					countDict[value]++;
				}
				else
				{
					countDict[value] = 1;
				}
			}

			if ( countDict.Count == 0 ) return "";
			int highestCount = countDict.Values.Max();

			List<string> valuesWithHighestCount = countDict
				.Where( kvp => kvp.Value == highestCount )
				.Select( kvp => kvp.Key )
				.ToList();

			if ( valuesWithHighestCount.Count >= 2 ) return "democracy_tie_exception";
			return valuesWithHighestCount.FirstOrDefault();
		}

		[GameEvent.Tick.Server]
		public void GameTick()
		{
			if ( IsActive )
			{
				// Only vote with three or more players.
				if ( LastVote > 60 && Game.Clients.Count( client => client.Pawn is TerrorTown.Player player && player.LifeState == LifeState.Alive ) >= 3 )
				{
					if ( Game.IsServer )
					{
						// Event name is long to prevent duplicates and its only getting used here.
						Event.Run( "minigamepack_client_open_democracy_ui" );
						LastVote = 0;
						StartedVote = 0;
						handled_vote = false;
					}
				}
				if ( StartedVote > vote_time && !handled_vote )
				{
					// This ugly block of code handles the vote checking and all outlier cases I could think of.
					var soon_dead_guy = get_most_voted( vote_list );
					if ( string.IsNullOrEmpty(soon_dead_guy) ) { PopupSystem.DisplayPopup( To.Everyone, $"No one voted!" ); vote_list = new Dictionary<string, string>(); handled_vote = true; return; }
					if (soon_dead_guy == "democracy_tie_exception" ) { PopupSystem.DisplayPopup( To.Everyone, $"It was a tie!" ); vote_list = new Dictionary<string, string>(); handled_vote = true; return; }
					if ( !Game.Clients.Any( x => x.Name == soon_dead_guy ) ) { PopupSystem.DisplayPopup( To.Everyone, $"Most voted person left." ); vote_list = new Dictionary<string, string>(); handled_vote = true; return; }

					var client = Game.Clients.First( x => x.Name == soon_dead_guy );
					var pawn = client.Pawn as TerrorTown.Player;

					if ( pawn == null ) { Log.Error( "person voted was not a player...?" ); vote_list = new Dictionary<string, string>(); handled_vote = true; return; }
					if ( pawn.LifeState != LifeState.Alive ) { PopupSystem.DisplayPopup( To.Everyone, $"Most voted person died before vote end." ); vote_list = new Dictionary<string, string>(); handled_vote = true; return;}

					// All checks cleared, go ahead and kill the guy!! >:)
					PopupSystem.DisplayPopup( To.Everyone, $"{soon_dead_guy} was voted to die!" );
					pawn.TakeDamage( (new DamageInfo { Damage = pawn.Health * 99 }).WithTag( "suicide" ) );
					vote_list = new Dictionary<string, string>(); 
					handled_vote = true; 
					return;
				}
			}
		}

		[Event( "minigamepack_client_open_democracy_ui" )]
		[ClientRpc]
		public static void OpenUI()
		{
			var pawn = Game.LocalClient.Pawn as TerrorTown.Player;
			if ( pawn.LifeState == LifeState.Alive )
			{
				var panel = Game.RootPanel.AddChild<DemocracyUI>();
				panel.Init( vote_time );
			}
		}

		[ConCmd.Server( "minigame_democracy_cast_vote" )]
		public static void CastVote( string name )
		{
			if (!handled_vote)
			{
				if (!vote_list.Keys.Contains(ConsoleSystem.Caller.Name))
				{
					var ply = ConsoleSystem.Caller.Pawn as TerrorTown.Player;
					if (ply.LifeState != LifeState.Alive ) { return; }
					if (Game.Clients.Any(x => x.Name == name))
					{
						vote_list[ConsoleSystem.Caller.Name] = name;
						Chat.AddChatEntry( To.Everyone, null,  ConsoleSystem.Caller.Name + " voted for " + name );
					}
				}
			}
		}
	}

	public class Moon : Minigame
	{
		public override string Name { get; set; } = "Moon shoes";
		public override string Description { get; set; } = "Gravity is lowered!";

		public override void RoundStart()
		{
			base.RoundStart();
			TerrorTown.WalkController.Gravity /= 3.2f ;
		}
		public override void RoundEnd()
		{
			base.RoundEnd();
			TerrorTown.WalkController.Gravity *= 3.2f;
		}
	}

	public class IceRink : Minigame
	{
		public override string Name { get; set; } = "Ice rink";
		public override string Description { get; set; } = "The map has frozen over and everyone is on iceskates! Friction is lowered.";
		public override void RoundStart()
		{
			base.RoundStart();
			TerrorTown.WalkController.GroundFriction /= 10f;
		}
		public override void RoundEnd()
		{
			base.RoundEnd();
			TerrorTown.WalkController.GroundFriction *= 10f;

		}
	}

	// Credits for the next three minigames go to StealthNinja1O1
	public class Hypersonic : Minigame
	{
		public override string Name { get; set; } = "I AM SPEED";
		public override string Description { get; set; } = "Every player becomes very fast.";

		public override void RoundStart()
		{
			base.RoundStart();

			foreach ( var client in Game.Clients )
			{
				var pawn = client.Pawn as TerrorTown.Player;
				if ( pawn == null ) continue;
				var movementController = pawn.MovementController as TerrorTown.WalkController;
				if ( movementController != null )  movementController.SpeedMultiplier = 2.2f;
			}
		}

		public override void RoundEnd()
		{
			base.RoundEnd();

			foreach ( var client in Game.Clients )
			{
				var pawn = client.Pawn as TerrorTown.Player;
				if ( pawn == null ) continue;
				var movementController = pawn.MovementController as TerrorTown.WalkController;
				if ( movementController != null ) { movementController.SpeedMultiplier = 1.0f; }
			}
		}
	}

	public class Shrink : Minigame
	{
		public override string Name { get; set; } = "Trouble in Smurf Village";
		public override string Description { get; set; } = "Every player becomes tiny.";

		public override void RoundStart()
		{
			base.RoundStart();

			foreach ( var client in Game.Clients )
			{
				var pawn = client.Pawn as TerrorTown.Player;
				if ( pawn == null ) continue;
				var movementController = pawn.MovementController as TerrorTown.WalkController;
				pawn.LocalScale = 0.5f;
				if ( movementController != null ) movementController.SpeedMultiplier = 1.5f;
			}
		}

		public override void RoundEnd()
		{
			base.RoundEnd();

			foreach ( var client in Game.Clients )
			{
				var pawn = client.Pawn as TerrorTown.Player;
				if ( pawn == null ) continue;
				var movementController = pawn.MovementController as TerrorTown.WalkController;
				pawn.LocalScale = 1.0f;
				if ( movementController != null ) { movementController.SpeedMultiplier = 1.0f; }
			}
		}
	}

	public class Nuke : ModelEntity
	{

		public override void Spawn()
		{
			base.Spawn();
			SetupModel();
			SetupPhysicsFromModel( PhysicsMotionType.Dynamic );
			PhysicsBody.Mass = 200f;
		}

		private void SetupModel()
		{
			SetModel( "models/fridge.vmdl" );
		}

		protected override void OnPhysicsCollision( CollisionEventData eventData )
		{
			// If it touches anything, explode
			var explode = new TerrorTown.ExplosionEntity();
			explode.Position = Position;
			explode.Radius = 100;
			explode.Damage = 100;
			explode.Radius = 250;
			explode.Explode( null );
			Delete();
		}
	}
	
	// S&box minigame
	public class Nukes : Minigame
	{
		public override string Name { get; set; } = "Nuclear warfare";
		public override string Description { get; set; } = "Nukes (inside fridges) are falling from the sky, avoid them!";

		private RealTimeSince lastNuke;

		private int nukeTime;

		public override void RoundStart()
		{
			base.RoundStart();
			lastNuke = 0;
			nukeTime = 30;
		}

		[GameEvent.Tick.Server]
		public void OnTick()
		{
			if ( !IsActive ) return;
			if ( lastNuke > nukeTime )
			{
				var count = Game.Clients.Count;
				var nukesToSpawn = Game.Random.Int( 1, count );
				for ( int i = 0; i < nukesToSpawn; i++ )
				{
					var nuke = new Nuke();

					var randomply = Utils.getAlivePlayer();
					// Spawn above player with some randomness
					nuke.Position = randomply.Position + new Vector3( Game.Random.Float( -100, 100 ), Game.Random.Float( -100, 100 ), 1000 );
				}
				lastNuke = 0;
				nukeTime = Game.Random.Int( 2, 60 );
			}

		}
	}
}
