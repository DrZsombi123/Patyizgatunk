using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

// Kaszinó játékok egy ablakban: nyerőgép, rulett, huszonegy (blackjack), kocka (kicsi/nagy).
// Nyitva a játék megáll (GetTree().Paused), ez a CanvasLayer viszont fut tovább (ProcessMode.Always),
// így nem kell a Player-ben/Car-ban külön figyelni, hogy épp játszunk-e.
// Nagy nyerés (legalább a tét ötszöröse) +10 aura.
// ponytail: a UI kódból épül, mint a Dialogue - egy CanvasLayer node a World-ben.
public partial class CasinoGame : CanvasLayer
{
	public static CasinoGame Current { get; private set; }

	public static bool IsOpen => Current != null && Current._root.Visible;

	private static readonly int[] Bets = { 500, 1000, 2000, 5000, 10000 };
	private static readonly string[] SlotIcons = { "patyi", "jack", "finlandia", "energia", "kulcs" };
	private static readonly string[] Ranks = { "", "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
	private static readonly string[] Suits = { "♠", "♥", "♦", "♣" };

	private static readonly Color Yellow = new Color(1, 1, 0);
	private static readonly Color White = new Color(1, 1, 1);
	private static readonly Color Green = new Color(0.35f, 1, 0.4f);
	private static readonly Color Red = new Color(1, 0.3f, 0.3f);
	private static readonly Color Felt = new Color(0.05f, 0.3f, 0.15f);
	private static readonly Color Gold = new Color(0.85f, 0.65f, 0.2f);

	private Control _root;
	private Label _title;
	private Label _money;
	private Label _betLabel;
	private Label _result;
	private PanelContainer _table;
	private VBoxContainer _body;
	private readonly List<Button> _betButtons = new();

	private Player _player;
	private string _game;
	private int _betIndex = 1;
	private int _stake;   // az épp futó kör tétje (a tétállító közben nem írja át)
	private bool _busy;   // animáció vagy félkész leosztás: nem lehet kilépni, új kört kezdeni
	private readonly RandomNumberGenerator _rng = new();

	private int Bet => Bets[_betIndex];

	public static string Title(string game) => game switch
	{
		"slots" => "Nyerőgép",
		"roulette" => "Rulett",
		"blackjack" => "Huszonegy",
		"dice" => "Kocka",
		_ => game,
	};

	public override void _Ready()
	{
		Current = this;
		Layer = 12;
		ProcessMode = ProcessModeEnum.Always;

		BuildUi();
		_root.Visible = false;
	}

	public override void _ExitTree()
	{
		if (Current == this)
			Current = null;
	}

	public void Open(string game, Player player)
	{
		_game = game;
		_player = player;
		_busy = false;

		_title.Text = Title(game).ToUpper();
		_root.Visible = true;
		GetTree().Paused = true;

		foreach (Node child in _body.GetChildren())
		{
			_body.RemoveChild(child);
			child.QueueFree();
		}

		switch (game)
		{
			case "slots": BuildSlots(); break;
			case "roulette": BuildRoulette(); break;
			case "blackjack": BuildBlackjack(); break;
			case "dice": BuildDice(); break;
		}

		ShowMoney();
		ShowBet();
	}

	private void Close()
	{
		if (_busy)
			return;

		_root.Visible = false;
		GetTree().Paused = false;
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsOpen || !@event.IsActionPressed("ui_cancel"))
			return;

		Close();
		GetViewport().SetInputAsHandled();
	}

	// ---------- közös: tét, pénz, eredmény ----------

	private bool TakeBet()
	{
		if (_busy)
			return false;

		if (_player.Money < Bet)
		{
			SetResult($"Nincs meg a {Ft(Bet)} tét.", Red);
			return false;
		}

		_stake = Bet;
		_player.Money -= _stake;
		ShowMoney();
		return true;
	}

	// win: a visszakapott teljes összeg (tét + nyeremény), 0 = vesztettünk
	private void Payout(int win, string text)
	{
		if (win <= 0)
		{
			SetResult(text, Red);
			return;
		}

		_player.Money += win;
		ShowMoney();

		if (win >= _stake * 5)
		{
			_player.AddAura(10);
			SetResult($"{text}  +{Ft(win)}  +10 AURA", Yellow);
			return;
		}

		SetResult($"{text}  +{Ft(win)}", Green);
	}

	private void ChangeBet(int step)
	{
		if (_busy)
			return;

		_betIndex = Mathf.Clamp(_betIndex + step, 0, Bets.Length - 1);
		ShowBet();
	}

	private void ShowBet() => _betLabel.Text = "Tét: " + Ft(Bet);

	private void ShowMoney() => _money.Text = Ft(_player.Money);

	private void SetResult(string text, Color color)
	{
		_result.Text = text;
		_result.AddThemeColorOverride("font_color", color);
	}

	private static string Ft(int amount) => amount.ToString("#,0").Replace(",", " ") + " Ft";

	// a játék áll (Paused), ezért a timernek process_always kell
	private async Task Wait(double seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds, true), SceneTreeTimer.SignalName.Timeout);
	}

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

	private static Texture2D SlotIcon(int index) => GD.Load<Texture2D>($"res://Art/item_{SlotIcons[index]}.png");

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

	// ---------- UI ----------

	private List<Button> AddActions(params (string Label, System.Action Pressed)[] actions)
	{
		var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		row.AddThemeConstantOverride("separation", 8);
		_body.AddChild(row);

		var buttons = new List<Button>();

		foreach (var (label, pressed) in actions)
		{
			Button button = NewButton(label);
			button.Pressed += () => pressed();
			row.AddChild(button);
			buttons.Add(button);
		}

		return buttons;
	}

	private void SetFelt(Color color) => _table.AddThemeStyleboxOverride("panel", Box(color, Gold, 10));

	private void BuildUi()
	{
		_root = new Control();
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
		dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(dim);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(center);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(760, 0) };
		panel.AddThemeStyleboxOverride("panel", Box(new Color(0, 0, 0, 0.92f), White, 6, 3));
		center.AddChild(panel);

		var inner = new MarginContainer();
		foreach (string side in new[] { "left", "right", "top", "bottom" })
			inner.AddThemeConstantOverride("margin_" + side, 16);
		panel.AddChild(inner);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 10);
		inner.AddChild(box);

		var header = new HBoxContainer();
		box.AddChild(header);

		_title = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_title.AddThemeFontSizeOverride("font_size", 26);
		_title.AddThemeColorOverride("font_color", Yellow);
		header.AddChild(_title);

		_money = new Label { VerticalAlignment = VerticalAlignment.Center };
		_money.AddThemeFontSizeOverride("font_size", 18);
		_money.AddThemeColorOverride("font_color", Green);
		header.AddChild(_money);

		_table = new PanelContainer();
		box.AddChild(_table);

		var tableInner = new MarginContainer();
		foreach (string side in new[] { "left", "right", "top", "bottom" })
			tableInner.AddThemeConstantOverride("margin_" + side, 14);
		_table.AddChild(tableInner);

		_body = new VBoxContainer();
		_body.AddThemeConstantOverride("separation", 10);
		tableInner.AddChild(_body);

		_result = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.Word,
			HorizontalAlignment = HorizontalAlignment.Center,
			CustomMinimumSize = new Vector2(0, 48),
		};
		_result.AddThemeFontSizeOverride("font_size", 18);
		box.AddChild(_result);

		var footer = new HBoxContainer();
		footer.AddThemeConstantOverride("separation", 8);
		box.AddChild(footer);

		Button less = NewButton("−");
		less.Pressed += () => ChangeBet(-1);
		footer.AddChild(less);

		_betLabel = new Label { VerticalAlignment = VerticalAlignment.Center, CustomMinimumSize = new Vector2(150, 0), HorizontalAlignment = HorizontalAlignment.Center };
		_betLabel.AddThemeFontSizeOverride("font_size", 18);
		footer.AddChild(_betLabel);

		Button more = NewButton("+");
		more.Pressed += () => ChangeBet(1);
		footer.AddChild(more);

		_betButtons.Add(less);
		_betButtons.Add(more);

		footer.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

		Button leave = NewButton("Felállok (Esc)");
		leave.Pressed += Close;
		footer.AddChild(leave);
	}

	private static Button NewButton(string label)
	{
		var button = new Button { Text = label, FocusMode = Control.FocusModeEnum.None };

		button.AddThemeFontSizeOverride("font_size", 17);
		button.AddThemeColorOverride("font_color", White);
		button.AddThemeColorOverride("font_hover_color", Colors.Black);
		button.AddThemeColorOverride("font_pressed_color", Colors.Black);
		button.AddThemeColorOverride("font_disabled_color", new Color(1, 1, 1, 0.3f));
		button.AddThemeStyleboxOverride("normal", Box(new Color(1, 1, 1, 0.08f), new Color(1, 1, 1, 0.4f), 4, 1, 12, 6));
		button.AddThemeStyleboxOverride("hover", Box(Yellow, Yellow, 4, 1, 12, 6));
		button.AddThemeStyleboxOverride("pressed", Box(Yellow, Yellow, 4, 1, 12, 6));
		button.AddThemeStyleboxOverride("disabled", Box(new Color(1, 1, 1, 0.03f), new Color(1, 1, 1, 0.15f), 4, 1, 12, 6));

		return button;
	}

	private static StyleBoxFlat Box(Color background, Color border, int radius, int borderWidth = 2, int marginX = 0, int marginY = 0)
	{
		var box = new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
			ContentMarginLeft = marginX,
			ContentMarginRight = marginX,
			ContentMarginTop = marginY,
			ContentMarginBottom = marginY,
		};

		box.SetBorderWidthAll(borderWidth);
		box.SetCornerRadiusAll(radius);
		return box;
	}
}
