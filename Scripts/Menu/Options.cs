using Godot;

// Beállítások: fő hangerő és V-sync. A jelek (value_changed, toggled) a scene-ben vannak bekötve.
public partial class Options : Control
{
	[Export] private HSlider _masterSlider;

	public override void _Ready()
	{
		// a csúszka a Master bus jelenlegi hangerejéről induljon
		if (_masterSlider != null)
			_masterSlider.Value = Mathf.DbToLinear(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master")));

		// a pipa mutassa a valós állapotot (alapból be van kapcsolva a V-sync)
		GetNode<CheckBox>("PanelContainer/VBoxContainer/CheckBox").SetPressedNoSignal(
			DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled);
	}

	private void OnMasterVolumeChanged(double value)
	{
		// 0-nál a LinearToDb -inf lenne
		float db = Mathf.LinearToDb(Mathf.Max((float)value, 0.0001f));
		AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), db);
	}

	private void OnVSyncToggled(bool isChecked)
	{
		DisplayServer.WindowSetVsyncMode(isChecked ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
	}
}
