using Godot;

// Belépéskor átteleportálja a playert a Target markerhez (épület be/ki).
public partial class Door : Area2D
{
	[Export]
	public Marker2D Target { get; set; }

	public override void _Ready()
	{
		BodyEntered += body =>
		{
			if (body is Player player)
				player.SetDeferred(Node2D.PropertyName.GlobalPosition, Target.GlobalPosition);
		};
	}
}
