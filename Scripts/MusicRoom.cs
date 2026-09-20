using Godot;

// Belső tér zenével: amíg a player bent van, szól a "Music" gyerek.
public partial class MusicRoom : Area2D
{
	public override void _Ready()
	{
		var music = GetNode<AudioStreamPlayer>("Music");

		BodyEntered += body => { if (body is Player) music.Play(); };
		BodyExited += body => { if (body is Player) music.Stop(); };
	}
}
