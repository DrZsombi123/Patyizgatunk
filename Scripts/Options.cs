using Godot;
using System;

public partial class Options : Control
{
	

	[Export] private HSlider _masterSlider;

    public override void _Ready()
    {
        if (_masterSlider != null)
        {
            // a value_changed a scene-ben már be van kötve, itt nem kell még egyszer

            // Beállítjuk a csúszka kezdeti pozícióját a jelenlegi Master bus hangerő alapján
            int masterBusIndex = AudioServer.GetBusIndex("Master");
            float currentDb = AudioServer.GetBusVolumeDb(masterBusIndex);
            _masterSlider.Value = Mathf.DbToLinear(currentDb);
        }

        // a pipa mutassa a valós állapotot (alapból be van kapcsolva a V-sync)
        GetNode<CheckBox>("PanelContainer/VBoxContainer/CheckBox").SetPressedNoSignal(
            DisplayServer.WindowGetVsyncMode() != DisplayServer.VSyncMode.Disabled);
    }

    private void OnMasterVolumeChanged(double value)
    {
        // 0.0001-nél nem engedjük kisebbre a biztonságos LinearToDb konverzió miatt
        float linearValue = Mathf.Max((float)value, 0.0001f);
        
        // Konvertáljuk a 0..1 közötti lineáris skálát decibelbe (dB)
        float dbValue = Mathf.LinearToDb(linearValue);

        // Állítjuk a Master audio bus hangerőjét
        int masterBusIndex = AudioServer.GetBusIndex("Master");
        AudioServer.SetBusVolumeDb(masterBusIndex, dbValue);
    }

	private void OnVSyncToggled(bool isChecked)
    {
        DisplayServer.WindowSetVsyncMode(isChecked 
            ? DisplayServer.VSyncMode.Enabled 
            : DisplayServer.VSyncMode.Disabled);
		GD.Print("Vsyncbtn teszt");
    }
}
