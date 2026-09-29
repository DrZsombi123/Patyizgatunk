using Godot;

// Részegség képi hatásai a "DrunkEffects" CanvasLayer-en:
// NauseaRect (nausea.gdshader: hullámzás, homály) és BlackoutRect (fekete, kiütéskor elsötétül).
public partial class DrunkEffects : CanvasLayer
{
	[Export] public DrunkSystem Drunk { get; set; }

	private ColorRect _nauseaRect;
	private ColorRect _blackoutRect;
	private ShaderMaterial _mat;
	private Tween _fadeTween;

	private float _targetIntensity, _targetBlindness;
	private float _intensity, _blindness;

	public override void _Ready()
	{
		_nauseaRect = GetNode<ColorRect>("NauseaRect");
		_blackoutRect = GetNode<ColorRect>("BlackoutRect");
		_mat = (ShaderMaterial)_nauseaRect.Material;

		_blackoutRect.Modulate = new Color(1, 1, 1, 0);

		Drunk.DrunkennessChanged += OnDrunkennessChanged;
		Drunk.BlackoutStarted += time => FadeBlackout(1f, time);
		Drunk.BlackoutEnded += time => FadeBlackout(0f, time);
		OnDrunkennessChanged(Drunk.Drunkness);
	}

	private void OnDrunkennessChanged(float value)
	{
		_targetIntensity = Mathf.Clamp(Mathf.InverseLerp(Drunk.NauseaStart, DrunkSystem.MaxDrunkness, value), 0f, 1f);
		_targetBlindness = Mathf.Pow(Mathf.Clamp(Mathf.InverseLerp(Drunk.ChaosStart, Drunk.BlindStart, value), 0f, 1f), 2f);
	}

	public override void _Process(double delta)
	{
		float k = 1f - Mathf.Exp(-3f * (float)delta); // képkocka-független simítás
		_intensity = Mathf.Lerp(_intensity, _targetIntensity, k);
		_blindness = Mathf.Lerp(_blindness, _targetBlindness, k);

		_nauseaRect.Visible = _intensity > 0.001f;
		_mat.SetShaderParameter("intensity", _intensity);
		_mat.SetShaderParameter("blindness", _blindness);
	}

	private void FadeBlackout(float alpha, float time)
	{
		_fadeTween?.Kill();
		_fadeTween = CreateTween();
		_fadeTween.TweenProperty(_blackoutRect, "modulate:a", alpha, time);
	}
}
