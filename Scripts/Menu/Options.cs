using Godot;

// Beállítások: fő hangerő és V-sync. A jelek (value_changed, toggled) a scene-ben vannak bekötve.
// A user://settings.cfg-be mentődnek, induláskor a TitleScreen tölti vissza (Apply).
public partial class Options : Control
{
	private const string FilePath = "user://settings.cfg";

	[Export] private HSlider _masterSlider;

	// induláskor: az elmentett beállítások érvényesítése
	public static void Apply()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(FilePath) != Error.Ok)
			return;

		SetVolume((float)cfg.GetValue("audio", "master", 1.0f));
		SetVSync((bool)cfg.GetValue("display", "vsync", true));
	}

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
		SetVolume((float)value);
		Save("audio", "master", value);
	}

	private void OnVSyncToggled(bool isChecked)
	{
		SetVSync(isChecked);
		Save("display", "vsync", isChecked);
	}

	private static void SetVolume(float value)
	{
		// 0-nál a LinearToDb -inf lenne
		float db = Mathf.LinearToDb(Mathf.Max(value, 0.0001f));
		AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), db);
	}

	private static void SetVSync(bool on)
	{
		DisplayServer.WindowSetVsyncMode(on ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
	}

	private static void Save(string section, string key, Variant value)
	{
		var cfg = new ConfigFile();
		cfg.Load(FilePath);   // ha még nincs, üresből indulunk
		cfg.SetValue(section, key, value);
		cfg.Save(FilePath);
	}
}
