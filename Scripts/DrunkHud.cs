using Godot;

/// <summary>
/// Attach to the CanvasLayer called "DrunkHUD".
/// Expects this child structure (names must match exactly):
///   MarginContainer/VBoxContainer/StatusLabel  (Label)
///   MarginContainer/VBoxContainer/DrunkBar     (ProgressBar)
/// </summary>
public partial class DrunkHud : CanvasLayer
{
	[Export] public DrunkSystem Drunk { get; set; } = null!;

	private ProgressBar _bar = null!;
	private Label _label = null!;
	private float _target;
	private float _shown;

	public override void _Ready()
	{
		if (Drunk == null)
		{
			GD.PushError("DrunkHUD: assign the 'Drunk' export in the Inspector.");
			SetProcess(false);
			return;
		}

		_bar = GetNode<ProgressBar>("MarginContainer/VBoxContainer/DrunkBar");
		_label = GetNode<Label>("MarginContainer/VBoxContainer/StatusLabel");

		_bar.MinValue = 0;
		_bar.MaxValue = DrunkSystem.MaxDrunkness;
		_bar.ShowPercentage = false;

		Drunk.DrunkennessChanged += OnDrunkennessChanged;
		OnDrunkennessChanged(Drunk.Drunkness);
		_shown = _target;
	}

	private void OnDrunkennessChanged(float value)
	{
		_target = value;
		_label.Text = $"{Drunk.GetStatusName()}  {Mathf.RoundToInt(value)}%";
	}

	public override void _Process(double delta)
	{
		// Smoothly slide the bar (makes the 100% -> 30% drop look nice)
		_shown = Mathf.MoveToward(_shown, _target, 60f * (float)delta);
		_bar.Value = _shown;
		_bar.Modulate = BarColor(_shown / DrunkSystem.MaxDrunkness);
	}

	// green -> yellow -> red
	private static Color BarColor(float t)
	{
		return t < 0.5f
			? Colors.LimeGreen.Lerp(Colors.Yellow, t * 2f)
			: Colors.Yellow.Lerp(Colors.OrangeRed, (t - 0.5f) * 2f);
	}
}