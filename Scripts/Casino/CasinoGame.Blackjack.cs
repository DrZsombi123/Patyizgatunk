using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class CasinoGame
{
	// ---------- huszonegy ----------

	private readonly List<(int Rank, int Suit)> _dealerHand = new();
	private readonly List<(int Rank, int Suit)> _playerHand = new();
	private HBoxContainer _dealerRow;
	private HBoxContainer _playerRow;
	private Label _dealerScore;
	private Label _playerScore;
	private Button _deal;
	private Button _hit;
	private Button _stand;
	private bool _hideHole;   // az osztó második lapja lefordítva, amíg meg nem állunk

	private void BuildBlackjack()
	{
		SetFelt(Felt);
		SetResult("Győzd le az osztót 21 alatt! Blackjack: x2,5. Az osztó 17-ig húz.", White);

		_dealerScore = HandLabel("Osztó");
		_dealerRow = HandRow();
		_playerScore = HandLabel("Te");
		_playerRow = HandRow();

		var buttons = AddActions(
			("Osztás", () => Deal()),
			("Lapot kérek", () => Hit()),
			("Megállok", () => _ = Stand()));

		(_deal, _hit, _stand) = (buttons[0], buttons[1], buttons[2]);
		_dealerHand.Clear();
		_playerHand.Clear();
		ShowHands();
	}

	private Label HandLabel(string who)
	{
		var label = new Label { Text = who };
		label.AddThemeFontSizeOverride("font_size", 16);
		_body.AddChild(label);
		return label;
	}

	private HBoxContainer HandRow()
	{
		var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 66) };
		row.AddThemeConstantOverride("separation", 6);
		_body.AddChild(row);
		return row;
	}

	private (int, int) Draw() => (_rng.RandiRange(1, 13), _rng.RandiRange(0, 3));

	private static int Score(List<(int Rank, int Suit)> hand)
	{
		int total = 0;
		int aces = 0;

		foreach (var card in hand)
		{
			total += card.Rank == 1 ? 11 : Mathf.Min(card.Rank, 10);
			aces += card.Rank == 1 ? 1 : 0;
		}

		// az ász 11-ből 1 lesz, ha különben besokallnánk
		for (; total > 21 && aces > 0; aces--)
			total -= 10;

		return total;
	}

	private void Deal()
	{
		if (!TakeBet())
			return;

		_busy = true;
		_hideHole = true;
		_dealerHand.Clear();
		_playerHand.Clear();
		_playerHand.Add(Draw());
		_dealerHand.Add(Draw());
		_playerHand.Add(Draw());
		_dealerHand.Add(Draw());
		SetResult("Kérsz még lapot?", White);
		ShowHands();

		if (Score(_playerHand) == 21)
		{
			_hideHole = false;
			bool push = Score(_dealerHand) == 21;
			EndHand(push ? _stake : _stake * 5 / 2, push ? "Mindkettőtöknek blackjack, döntetlen." : "BLACKJACK!");
		}
	}

	private void Hit()
	{
		_playerHand.Add(Draw());
		ShowHands();

		if (Score(_playerHand) > 21)
		{
			_hideHole = false;
			EndHand(0, $"Besokalltál ({Score(_playerHand)}).");
		}
	}

	private async Task Stand()
	{
		_hideHole = false;
		ShowHands();
		SetEnabled(false, false, false);

		while (Score(_dealerHand) < 17)
		{
			await Wait(0.5);
			_dealerHand.Add(Draw());
			ShowHands();
		}

		int me = Score(_playerHand);
		int dealer = Score(_dealerHand);

		if (dealer > 21 || me > dealer)
			EndHand(_stake * 2, dealer > 21 ? $"Az osztó besokallt ({dealer})!" : $"{me} a {dealer} ellen, nyertél!");
		else if (me == dealer)
			EndHand(_stake, $"{me}–{dealer}, döntetlen. Visszakapod a téted.");
		else
			EndHand(0, $"{me} a {dealer} ellen, az osztó nyert.");
	}

	private void EndHand(int win, string text)
	{
		_busy = false;
		ShowHands();
		Payout(win, text);
	}

	private void ShowHands()
	{
		FillRow(_dealerRow, _dealerHand, _hideHole);
		FillRow(_playerRow, _playerHand, false);

		_playerScore.Text = _playerHand.Count > 0 ? $"Te: {Score(_playerHand)}" : "Te";
		_dealerScore.Text = _dealerHand.Count == 0 ? "Osztó" : _hideHole ? $"Osztó: {Score(_dealerHand.GetRange(0, 1))} + ?" : $"Osztó: {Score(_dealerHand)}";

		bool playing = _busy && _hideHole;
		SetEnabled(!_busy, playing, playing);
	}

	private void SetEnabled(bool deal, bool hit, bool stand)
	{
		_deal.Disabled = !deal;
		_hit.Disabled = !hit;
		_stand.Disabled = !stand;
		foreach (Button button in _betButtons)
			button.Disabled = _busy;
	}

	private static void FillRow(HBoxContainer row, List<(int Rank, int Suit)> hand, bool hideSecond)
	{
		foreach (Node child in row.GetChildren())
		{
			row.RemoveChild(child);
			child.QueueFree();
		}

		for (int i = 0; i < hand.Count; i++)
		{
			bool hidden = hideSecond && i == 1;
			bool red = hand[i].Suit == 1 || hand[i].Suit == 2;

			var card = new PanelContainer { CustomMinimumSize = new Vector2(46, 64) };
			card.AddThemeStyleboxOverride("panel", Box(hidden ? new Color(0.1f, 0.2f, 0.6f) : White, hidden ? White : new Color(0.6f, 0.6f, 0.6f), 5));

			var label = new Label
			{
				Text = hidden ? "?" : $"{Ranks[hand[i].Rank]}\n{Suits[hand[i].Suit]}",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
			};
			label.AddThemeFontSizeOverride("font_size", 18);
			label.AddThemeColorOverride("font_color", hidden ? White : red ? new Color(0.8f, 0.05f, 0.1f) : Colors.Black);
			card.AddChild(label);

			row.AddChild(card);
		}
	}
}
