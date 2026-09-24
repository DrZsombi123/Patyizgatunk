using Godot;
using System.Threading.Tasks;

public partial class CasinoGame
{
	// ---------- rulett ----------

	private RouletteWheel _wheel;
	private Label _number;
	private PanelContainer _numberBox;

	private void BuildRoulette()
	{
		SetFelt(Felt);
		SetResult("Válassz, mire teszel! A tétet a pörgetés előtt állítsd.", White);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 28);
		_body.AddChild(row);

		_wheel = new RouletteWheel { CustomMinimumSize = new Vector2(250, 250) };
		row.AddChild(_wheel);

		// a nyerőszám nagyban a kerék mellett
		_numberBox = new PanelContainer { CustomMinimumSize = new Vector2(110, 90), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		row.AddChild(_numberBox);

		_number = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
		_number.AddThemeFontSizeOverride("font_size", 48);
		_numberBox.AddChild(_number);
		PaintNumber(-1);

		AddActions(
			("Piros x2", () => _ = Roulette("Piros", n => IsRed(n), 2)),
			("Fekete x2", () => _ = Roulette("Fekete", n => n != 0 && !IsRed(n), 2)),
			("Páros x2", () => _ = Roulette("Páros", n => n != 0 && n % 2 == 0, 2)),
			("Páratlan x2", () => _ = Roulette("Páratlan", n => n % 2 == 1, 2)));

		AddActions(
			("1–18 x2", () => _ = Roulette("1–18", n => n >= 1 && n <= 18, 2)),
			("19–36 x2", () => _ = Roulette("19–36", n => n >= 19, 2)),
			("Zöld 0 x36", () => _ = Roulette("Zöld 0", n => n == 0, 36)));
	}

	private static bool IsRed(int number) => RouletteWheel.IsRed(number);

	private void PaintNumber(int number)
	{
		Color color = number < 0 ? new Color(0.15f, 0.15f, 0.15f) : number == 0 ? new Color(0, 0.55f, 0.2f) : IsRed(number) ? new Color(0.75f, 0.05f, 0.1f) : Colors.Black;
		_numberBox.AddThemeStyleboxOverride("panel", Box(color, Gold, 10));
		_number.Text = number < 0 ? "?" : number.ToString();
	}

	private async Task Roulette(string name, System.Func<int, bool> wins, int multiplier)
	{
		if (!TakeBet())
			return;

		_busy = true;
		SetResult($"{name}... Nincs több tét!", White);
		PaintNumber(-1);

		int result = _rng.RandiRange(0, 36);
		await _wheel.Spin(result);
		PaintNumber(result);

		_busy = false;
		Payout(wins(result) ? _stake * multiplier : 0, $"{result}: " + (wins(result) ? "nyertél!" : "vesztettél."));
	}
}
