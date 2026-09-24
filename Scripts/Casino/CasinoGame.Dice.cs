using Godot;
using System.Threading.Tasks;

public partial class CasinoGame
{
	// ---------- kocka ----------

	private Label[] _dice;

	private void BuildDice()
	{
		SetFelt(Felt);
		SetResult("Három kocka. Kicsi: 4–10, Nagy: 11–17 (x2). Hármas: x30, és ilyenkor a kicsi/nagy veszít.", White);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 16);
		_body.AddChild(row);

		_dice = new Label[3];

		for (int i = 0; i < 3; i++)
		{
			var box = new PanelContainer { CustomMinimumSize = new Vector2(80, 80) };
			box.AddThemeStyleboxOverride("panel", Box(White, Gold, 12));
			row.AddChild(box);

			_dice[i] = new Label { Text = "?", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
			_dice[i].AddThemeFontSizeOverride("font_size", 44);
			_dice[i].AddThemeColorOverride("font_color", Colors.Black);
			box.AddChild(_dice[i]);
		}

		AddActions(
			("Kicsi x2", () => _ = Roll("Kicsi", (sum, triple) => !triple && sum <= 10, 2)),
			("Nagy x2", () => _ = Roll("Nagy", (sum, triple) => !triple && sum >= 11, 2)),
			("Hármas x30", () => _ = Roll("Hármas", (sum, triple) => triple, 30)));
	}

	private async Task Roll(string name, System.Func<int, bool, bool> wins, int multiplier)
	{
		if (!TakeBet())
			return;

		_busy = true;
		SetResult($"{name}... Gurul!", White);

		int[] final = new int[3];

		for (int step = 0; step < 14; step++)
		{
			for (int i = 0; i < 3; i++)
			{
				final[i] = _rng.RandiRange(1, 6);
				_dice[i].Text = final[i].ToString();
			}

			await Wait(0.05 + step * 0.01);
		}

		int sum = final[0] + final[1] + final[2];
		bool triple = final[0] == final[1] && final[1] == final[2];

		_busy = false;
		Payout(wins(sum, triple) ? _stake * multiplier : 0, $"Összesen {sum}{(triple ? " (hármas)" : "")}: " + (wins(sum, triple) ? "nyertél!" : "vesztettél."));
	}
}
