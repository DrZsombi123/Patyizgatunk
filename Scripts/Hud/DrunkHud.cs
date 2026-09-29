using Godot;

// Részegség csík és állapot felirat a "DrunkHUD" CanvasLayer-en
// (gyerekek: MarginContainer/VBoxContainer/StatusLabel és DrunkBar).
public partial class DrunkHud : CanvasLayer
{
	[Export] public DrunkSystem Drunk { get; set; }

	private ProgressBar _bar;
	private Label _label;
	private float _target;
	private float _shown;

	public override void _Ready()
	{
		_bar = GetNode<ProgressBar>("MarginContainer/VBoxContainer/DrunkBar");
		_label = GetNode<Label>("MarginContainer/VBoxContainer/StatusLabel");

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
		// a csík csúszik, hogy a kiütés utáni 100% -> 30% esés látszódjon
		_shown = Mathf.MoveToward(_shown, _target, 60f * (float)delta);
		_bar.Value = _shown;

		// zöld -> sárga -> piros
		float t = _shown / DrunkSystem.MaxDrunkness;
		_bar.Modulate = t < 0.5f
			? Colors.LimeGreen.Lerp(Colors.Yellow, t * 2f)
			: Colors.Yellow.Lerp(Colors.OrangeRed, (t - 0.5f) * 2f);
	}
}
