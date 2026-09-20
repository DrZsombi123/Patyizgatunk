using Godot;
using System;

public partial class Options : Control
{
	

	[Export] private HSlider _masterSlider;

    public override void _Ready()
    {
        if (_masterSlider != null)
        {
            // Bekötjük a csúszka értékváltozásának eseményét (signal)
            _masterSlider.ValueChanged += OnMasterVolumeChanged;

            // Beállítjuk a csúszka kezdeti pozícióját a jelenlegi Master bus hangerő alapján
            int masterBusIndex = AudioServer.GetBusIndex("Master");
            float currentDb = AudioServer.GetBusVolumeDb(masterBusIndex);
            _masterSlider.Value = Mathf.DbToLinear(currentDb);
        }
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
