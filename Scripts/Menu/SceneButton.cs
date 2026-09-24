using Godot;

// Menügomb: megnyomva a Scene jelenetre vált, üres Scene-nél kilép a játékból.
// (A régi BackButton / OptionsButton / CreditsButton / ExitButton helyett.)
public partial class SceneButton : Button
{
	[Export(PropertyHint.File, "*.tscn")]
	public string Scene { get; set; } = "";

	public override void _Ready()
	{
		Pressed += () =>
		{
			if (Scene == "")
				GetTree().Quit();
			else
				GetTree().ChangeSceneToFile(Scene);
		};
	}
}
