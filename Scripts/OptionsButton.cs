using Godot;
using System;

public partial class OptionsButton : Button
{
	

	public void OnOptionsPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Options.tscn");
	}
}
