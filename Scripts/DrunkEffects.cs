using Godot;

/// <summary>
/// Attach to the CanvasLayer called "DrunkEffects".
/// Expects these children (names must match exactly):
///   NauseaRect    (ColorRect with a ShaderMaterial using nausea.gdshader)
///   BlackoutRect  (ColorRect, black)
/// </summary>
public partial class DrunkEffects : CanvasLayer
{
	[Export] public DrunkSystem Drunk { get; set; } = null!;

	private ColorRect _nauseaRect = null!;
	private ColorRect _blackoutRect = null!;
	private ShaderMaterial _mat = null!;
	private Tween? _fadeTween;

	private float _targetIntensity, _targetBlindness;
	private float _intensity, _blindness;

	public override void _Ready()
	{
		if (Drunk == null)
		{
			GD.PushError("DrunkEffects: assign the 'Drunk' export in the Inspector.");
			SetProcess(false);
			return;
		}

		_nauseaRect = GetNode<ColorRect>("NauseaRect");
		_blackoutRect = GetNode<ColorRect>("BlackoutRect");
		_mat = (ShaderMaterial)_nauseaRect.Material;

		_blackoutRect.Modulate = new Color(1, 1, 1, 0);

		Drunk.DrunkennessChanged += OnDrunkennessChanged;
		Drunk.BlackoutStarted += OnBlackoutStarted;
		Drunk.BlackoutEnded += OnBlackoutEnded;
		OnDrunkennessChanged(Drunk.Drunkness);
	}

	private void OnDrunkennessChanged(float value)
{
	_targetIntensity = Mathf.Clamp(
		Mathf.InverseLerp(Drunk.NauseaStart, DrunkSystem.MaxDrunkness, value), 0f, 1f);

	_targetBlindness = Mathf.Pow(Mathf.Clamp(
		Mathf.InverseLerp(Drunk.ChaosStart, Drunk.BlindStart, value), 0f, 1f), 2f);
}

	public override void _Process(double delta)
	{
		float k = 1f - Mathf.Exp(-3f * (float)delta); // frame-rate independent smoothing
		_intensity = Mathf.Lerp(_intensity, _targetIntensity, k);
		_blindness = Mathf.Lerp(_blindness, _targetBlindness, k);

		_nauseaRect.Visible = _intensity > 0.001f;
		_mat.SetShaderParameter("intensity", _intensity);
		_mat.SetShaderParameter("blindness", _blindness);
	}

	private void OnBlackoutStarted(float fadeTime) => FadeBlackout(1f, fadeTime);
	private void OnBlackoutEnded(float fadeTime) => FadeBlackout(0f, fadeTime);

	private void FadeBlackout(float alpha, float time)
	{
		_fadeTween?.Kill();
		_fadeTween = CreateTween();
		_fadeTween.TweenProperty(_blackoutRect, "modulate:a", alpha, time);
	}
}