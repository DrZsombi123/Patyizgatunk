using Godot;

// Amíg ez a hang szól, lehalkítja a helyiség zenéjét, utána visszaengedi.
// ponytail: az AudioStreamPlayer-nek nincs "elindult" jelzése, ezért a Playing flaget
// figyeljük - így nem kell hozzányúlni ahhoz, aki elindítja (Npc.Sound).
public partial class Ducker : AudioStreamPlayer
{
	[Export]
	public AudioStreamPlayer Music { get; set; }

	[Export]
	public float DuckDb { get; set; } = -18.0f;

	[Export]
	public float Speed { get; set; } = 8.0f;   // mennyire gyorsan úszik át

	private float _normalDb;

	public override void _Ready()
	{
		if (Music != null)
			_normalDb = Music.VolumeDb;
	}

	public override void _Process(double delta)
	{
		if (Music == null)
			return;

		float target = Playing ? _normalDb + DuckDb : _normalDb;

		Music.VolumeDb = Mathf.Lerp(Music.VolumeDb, target, Mathf.Min(1.0f, (float)delta * Speed));
	}
}
