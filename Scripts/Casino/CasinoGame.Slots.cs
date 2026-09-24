using Godot;
using System.Threading.Tasks;

public partial class CasinoGame
{
	// ---------- nyerőgép ----------

	private TextureRect[] _reels;

	private void BuildSlots()
	{
		SetFelt(new Color(0.35f, 0.02f, 0.05f));
		SetResult("Három egyforma: x8, három patyi: x20. Kettő egyforma: visszakapod a téted.", White);

		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 16);
		_body.AddChild(row);

		_reels = new TextureRect[3];

		for (int i = 0; i < 3; i++)
		{
			var window = new PanelContainer();
			window.AddThemeStyleboxOverride("panel", Box(White, Gold, 8));
			row.AddChild(window);

			_reels[i] = new TextureRect
			{
				Texture = SlotIcon(i),
				CustomMinimumSize = new Vector2(96, 96),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			};
			window.AddChild(_reels[i]);
		}

		AddActions(("Pörgetés", () => _ = Spin()));
	}

	private static Texture2D SlotIcon(int index) => GD.Load<Texture2D>($"res://Art/Items/{SlotIcons[index]}.png");

	private async Task Spin()
	{
		if (!TakeBet())
			return;

		_busy = true;
		SetResult("Pörög...", White);

		int[] final = new int[3];
		for (int i = 0; i < 3; i++)
			final[i] = _rng.RandiRange(0, SlotIcons.Length - 1);

		// a hengerek egymás után állnak meg
		for (int step = 0; step < 26; step++)
		{
			for (int i = 0; i < 3; i++)
				_reels[i].Texture = SlotIcon(step < 10 + i * 8 ? _rng.RandiRange(0, SlotIcons.Length - 1) : final[i]);

			await Wait(0.06);
		}

		_busy = false;

		if (final[0] == final[1] && final[1] == final[2])
			Payout(_stake * (SlotIcons[final[0]] == "patyi" ? 20 : 8), "JACKPOT!");
		else if (final[0] == final[1] || final[1] == final[2] || final[0] == final[2])
			Payout(_stake, "Kettő egyforma, megvan a tét.");
		else
			Payout(0, "Semmi. A gép nyert.");
	}
}
