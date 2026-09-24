using Godot;

// Van mentés: a gomb "Folytatás" lesz, alá kerül egy "Új játék" (az törli a mentést).
public partial class PlayButton : Button
{
	public override void _Ready()
	{
		if (!SaveGame.Exists)
			return;

		Text = "Folytatás";

		// ugyanaz a kinézet, de szkript és bekötött jel nélkül
		var fresh = (Button)Duplicate(0);
		fresh.Text = "Új játék";
		fresh.Pressed += () =>
		{
			SaveGame.Delete();
			OnPlayPressed();
		};

		// deferred: a szülő épp a gyerekeit készíti elő
		Callable.From(() => AddSibling(fresh)).CallDeferred();
	}

	public void OnPlayPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/World.tscn");
	}
}
