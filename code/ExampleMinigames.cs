using System.Linq;
using Sandbox;
using TerrorTown;
using SM1Minigames;


// This file contains the example minigames that come with the launcher. Use this file to see how making a minigame works.
// There are many events you can listen to that are related to TTT, to find out more about that join the Three Thieves discord or look at the three thieves TTT wiki.

public class EveryoneRadar : Minigame
{
	public override string Name { get; set; } = "Radar field";

	public override string Description { get; set; } = "Every player gets a radar!";
	public override void RoundStart()
	{
		base.RoundStart();
		// This loops over all clients and thus all players.
		foreach (IClient client in Game.Clients )
		{
			var ply = client.Pawn as TerrorTown.Player;
			// Can't just add to inventory, because radar has custom functions when picked up.
			var radar = new Radar();
			radar.Position = ply.Position;
		}
	}
}

public class EveryoneDisguiser : Minigame
{
	public override string Name { get; set; } = "Who's who?";

	public override string Description { get; set; } = "Every player gets a disguiser!";
	public override void RoundStart()
	{
		base.RoundStart();
		// This loops over all clients and thus all players.
		foreach ( IClient client in Game.Clients )
		{
			var ply = client.Pawn as TerrorTown.Player;
			// Can't just add to inventory, because disguiser has custom functions when picked up.
			var disguiser = new Disguiser();
			disguiser.Position = ply.Position;
		}
	}
}

public class EveryonePoltergeist : Minigame
{
	public override string Name { get; set; } = "Total chaos!";

	public override string Description { get; set; } = "Every player gets a poltergeist!";
	public override void RoundStart()
	{
		base.RoundStart();
		Log.Info( "Polter Time!" + IsActive);
		// This loops over all clients and thus all players.
		foreach ( IClient client in Game.Clients )
		{
			var ply = client.Pawn as TerrorTown.Player;
			var disguiser = new Poltergeist();
			disguiser.Position = ply.Position;
		}
	}
}

public class Explode : Minigame
{
	public override string Name { get; set; } = "Living bombs";

	public override string Description { get; set; } = "Every 40 seconds a player explodes!";

	private static RealTimeSince lastexplosion { get; set; }
	public override void RoundStart()
	{
		base.RoundStart();
		// Initialise the explosion timer.
		lastexplosion = 0;
	}

	// Your minigame will only listen to events when it is active.
	[GameEvent.Tick.Server]
	public void Gametick()
	{
		// This is a custom function, so we need to manually check IsActive! (Not strictly necessary, but good practice.)
		if (IsActive)
		{
			// If it is more than 30 seconds since our last explosion.
			if(lastexplosion > 40)
			{
				lastexplosion = 0;
				// This selects a random player
				var randomindex = Game.Random.Int( Game.Clients.Count - 1 );
				var randomply = Game.Clients.ElementAt( randomindex ).Pawn as TerrorTown.Player;
				bool foundAlive = randomply.LifeState == LifeState.Alive;

				// This loop makes sure we only explode players who are alive.
				while ( !foundAlive )
				{
					randomindex = Game.Random.Int( Game.Clients.Count - 1 );
					randomply = Game.Clients.ElementAt( randomindex ).Pawn as TerrorTown.Player;
					foundAlive = randomply.LifeState == LifeState.Alive;
				}
				
				// This explodes that player >:)
				var exploder = new ExplosionEntity();
				exploder.Damage = 200f;
				exploder.Position = randomply.Position;
				exploder.Radius *= 2;
				exploder.RemoveOnExplode = true;
				exploder.Explode(null);
			}
		}
	}
}

public class AnnounceDeath : Minigame
{
	public override string Name { get; set; } = "No privacy";

	public override string Description { get; set; } = "Whenever a player dies, it gets announced to all other players!";

	// This is a TTT specific event. To learn more about these, visit the Three Thieves discord or the Three Thieves wiki.
	[Event("Player.PostOnKilled")]
	public void OnDeath(DamageInfo last, TerrorTown.Player ply)
	{
		// This is a custom function, so we need to manually check IsActive! (Not strictly necessary, but good practice.)
		if ( !IsActive) return;

		TerrorTown.PopupSystem.DisplayPopup( To.Everyone, $"{ply.Owner.Name} died!" );
	}
}

public class InfiniteAmmo : Minigame
{   
	public override string Name { get; set; } = "Infinite ammo!";

	public override string Description { get; set; } = "Every player has an infinite clip.";

	public override void RoundStart()
	{
		base.RoundStart();
		Weapon.sv_infinite_ammo = true;
	}

	public override void RoundEnd()
	{
		base.RoundEnd();
		Weapon.sv_infinite_ammo = false;
	}
}
