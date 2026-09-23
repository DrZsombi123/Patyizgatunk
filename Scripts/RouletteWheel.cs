using Godot;
using System.Threading.Tasks;

// Rajzolt európai rulettkerék (37 zseb) golyóval. Spin(szám): a kerék az egyik irányba,
// a golyó a másikba pörög, lassul, beesik és a megadott szám zsebében áll meg.
// ponytail: az eredményt a hívó sorsolja, a kerék csak eljátssza - így a kifizetés és
// az animáció nem csúszhat szét.
public partial class RouletteWheel : Control
{
	// a zsebek sorrendje a keréken (európai)
	private static readonly int[] Order =
	{
		0, 32, 15, 19, 4, 21, 2, 25, 17, 34, 6, 27, 13, 36, 11, 30, 8, 23, 10,
		5, 24, 16, 33, 1, 20, 14, 31, 9, 22, 18, 29, 7, 28, 12, 35, 3, 26,
	};

	private const float Pocket = Mathf.Tau / 37.0f;

	private static readonly Color Wood = new Color(0.36f, 0.2f, 0.09f);
	private static readonly Color Gold = new Color(0.85f, 0.65f, 0.2f);
	private static readonly Color Track = new Color(0.12f, 0.08f, 0.05f);
	private static readonly Color RedPocket = new Color(0.75f, 0.05f, 0.1f);
	private static readonly Color GreenPocket = new Color(0, 0.55f, 0.2f);

	private float _wheel = 0.0f;       // a kerék elfordulása (rad)
	private float _ball = -Mathf.Pi / 2;   // a golyó szöge (abszolút, rad)
	private float _ballRadius = 0.89f; // a golyó távolsága a középtől, a sugár arányában
	private bool _ballVisible = false;

	private float _wheelFrom, _wheelTurns, _ballFrom, _ballTurns, _radiusFrom;

	public static bool IsRed(int number) =>
		System.Array.IndexOf(new[] { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 }, number) >= 0;

	public async Task Spin(int result, float seconds = 5.0f)
	{
		var rng = new RandomNumberGenerator();

		_wheelFrom = _wheel;
		_wheelTurns = rng.RandfRange(2.0f, 3.0f) * Mathf.Tau;
		_ballFrom = _ball;
		_radiusFrom = _ballRadius;

		// visszafelé számolva: annyit forogjon a golyó, hogy a végén pont a kerék "result" zsebében álljon
		float target = _wheelFrom + _wheelTurns + System.Array.IndexOf(Order, result) * Pocket;
		_ballTurns = 4 * Mathf.Tau + Mathf.PosMod(_ballFrom - target, Mathf.Tau);
		_ballVisible = true;

		Tween tween = CreateTween();
		tween.TweenMethod(Callable.From<float>(Animate), 0.0f, 1.0f, seconds);
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private void Animate(float t)
	{
		float wheelEase = 1.0f - Mathf.Pow(1.0f - t, 3.0f);
		float ballEase = 1.0f - Mathf.Pow(1.0f - t, 2.2f);

		_wheel = _wheelFrom + _wheelTurns * wheelEase;
		_ball = _ballFrom - _ballTurns * ballEase;

		// a pálya szélén fut, 65%-nál lelassul és pattogva beesik a zsebek közé
		if (t < 0.65f)
			_ballRadius = Mathf.Lerp(_radiusFrom, 0.89f, Mathf.Min(t / 0.08f, 1.0f));   // az előző zsebből kiugrik a pályára
		else
		{
			float drop = Mathf.Clamp((t - 0.65f) / 0.25f, 0.0f, 1.0f);
			float bounce = Mathf.Abs(Mathf.Sin(drop * Mathf.Pi * 3.0f)) * (1.0f - drop) * 0.06f;
			_ballRadius = Mathf.Lerp(0.89f, 0.67f, Mathf.SmoothStep(0.0f, 1.0f, drop)) + bounce;
		}

		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 c = Size / 2;
		float r = Mathf.Min(Size.X, Size.Y) / 2 - 2;

		DrawCircle(c, r, Wood);
		DrawArc(c, r - 1, 0, Mathf.Tau, 64, Gold, 2, true);
		DrawCircle(c, r * 0.94f, Track);

		// zsebek: gyűrűcikkek a számokkal
		float inner = r * 0.62f;
		float outer = r * 0.84f;

		for (int i = 0; i < Order.Length; i++)
		{
			int number = Order[i];
			float mid = _wheel + i * Pocket;
			Color color = number == 0 ? GreenPocket : IsRed(number) ? RedPocket : Colors.Black;

			DrawColoredPolygon(Wedge(c, inner, outer, mid - Pocket / 2, mid + Pocket / 2), color);
			DrawLine(c + Vector2.FromAngle(mid - Pocket / 2) * inner, c + Vector2.FromAngle(mid - Pocket / 2) * outer, Gold, 1);
		}

		// számok a zsebek fölött, kifelé olvashatóan
		Font font = GetThemeDefaultFont();
		for (int i = 0; i < Order.Length; i++)
		{
			float mid = _wheel + i * Pocket;
			DrawSetTransform(c + Vector2.FromAngle(mid) * r * 0.77f, mid + Mathf.Pi / 2);
			DrawString(font, new Vector2(-10, 4), Order[i].ToString(), HorizontalAlignment.Center, 20, 10, Colors.White);
		}
		DrawSetTransform(Vector2.Zero, 0);

		DrawArc(c, outer, 0, Mathf.Tau, 64, Gold, 1.5f, true);
		DrawArc(c, inner, 0, Mathf.Tau, 64, Gold, 1.5f, true);

		// közép: fa kúp, arany kereszt (ez forog a kerékkel, látszik a pörgés)
		DrawCircle(c, inner, new Color(0.45f, 0.26f, 0.12f));
		DrawCircle(c, inner * 0.55f, Wood);
		for (int k = 0; k < 4; k++)
		{
			Vector2 arm = Vector2.FromAngle(_wheel + k * Mathf.Pi / 2) * inner * 0.8f;
			DrawLine(c, c + arm, Gold, 3, true);
			DrawCircle(c + arm, 3, Gold);
		}
		DrawCircle(c, 6, Gold);

		if (_ballVisible)
		{
			Vector2 ball = c + Vector2.FromAngle(_ball) * r * _ballRadius;
			DrawCircle(ball + new Vector2(1, 1), r * 0.04f, new Color(0, 0, 0, 0.5f));
			DrawCircle(ball, r * 0.04f, Colors.White);
		}
	}

	private static Vector2[] Wedge(Vector2 c, float inner, float outer, float from, float to)
	{
		const int steps = 4;
		var points = new Vector2[(steps + 1) * 2];

		for (int k = 0; k <= steps; k++)
		{
			float a = Mathf.Lerp(from, to, k / (float)steps);
			points[k] = c + Vector2.FromAngle(a) * outer;
			points[points.Length - 1 - k] = c + Vector2.FromAngle(a) * inner;
		}

		return points;
	}
}
