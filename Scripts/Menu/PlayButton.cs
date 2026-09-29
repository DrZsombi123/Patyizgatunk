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

		// első kattintásra csak rákérdez, a második törli a mentést; ha elmegyünk róla, visszaáll
		fresh.Pressed += () =>
		{
			if (fresh.Text == "Új játék")
			{
				fresh.Text = "Biztos? Mentés törlése";
				return;
			}

			SaveGame.Delete();
			OnPlayPressed();
		};
		fresh.MouseExited += () => fresh.Text = "Új játék";
		fresh.FocusExited += () => fresh.Text = "Új játék";

		// deferred: a szülő épp a gyerekeit készíti elő
		Callable.From(() => AddSibling(fresh)).CallDeferred();
	}

	public void OnPlayPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/World.tscn");
	}
}
