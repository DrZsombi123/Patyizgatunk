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
		Quests.Report("kaszino", win - _stake);   // csak a tiszta nyereség számít

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
