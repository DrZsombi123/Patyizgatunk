using Godot;

// Küldetések egymás után: mindig egy aktív, a jobb felső sarokban látszik a haladással.
// A játék többi része csak jelent, pl. Quests.Report("pia"). Teljesítéskor aura jár és jön a következő.
// A Toast rövid felirat a képernyő tetején (küldetés kész, rendőrség...).
// ponytail: a UI kódból épül, mint a Dialogue - egy CanvasLayer node a World-ben.
public partial class Quests : CanvasLayer
{
	public static Quests Current { get; private set; }

	private static readonly Color Yellow = new Color(1, 1, 0);
	private static readonly Color White = new Color(1, 1, 1);

	// felirat, esemény, cél, jutalom (aura)
	private static readonly (string Title, string Event, int Target, int Aura)[] List =
	{
		("Kérd el Brendontól a kocsikulcsot", "kulcs", 1, 10),
		("Vezess a mercivel (mp)", "vezetes", 30, 10),
		("Vágasd le a hajad a fodrásznál", "hajvagas", 1, 10),
		("Köszönj a nézőknek élőben", "koszones", 3, 10),
		("Szerezz rózsát TikTok élőben", "rozsa", 300, 20),
		("Igyál kemény piát", "pia", 3, 10),
		("Nyerj pénzt a kaszinóban (Ft)", "kaszino", 20000, 25),
		("Rázd le a rendőröket", "menekules", 1, 30),
	};

	public int Index { get; private set; }
	public int Progress { get; private set; }

	private Player _player;
	private Label _title;
	private Label _progress;
	private Label _toast;
	private Tween _toastTween;

	public override void _Ready()
	{
		Current = this;
		Layer = 11;   // a kiütés elsötétítése (10) fölött, hogy az "Elkaptak!" is látsszon

		_player = GetNode<Player>("../Player");

		BuildUi();
		Refresh();
	}

	public override void _ExitTree()
	{
		if (Current == this)
			Current = null;
	}

	public static void Report(string eventName, int amount = 1) => Current?.Advance(eventName, amount);

	public static void Toast(string text, Color color) => Current?.ShowToast(text, color);

	// mentésből
	public void Restore(int index, int progress)
	{
		Index = Mathf.Clamp(index, 0, List.Length);
		Progress = Mathf.Max(progress, 0);
		Refresh();
	}

	private void Advance(string eventName, int amount)
	{
		if (Index >= List.Length || amount <= 0 || List[Index].Event != eventName)
			return;

		Progress += amount;

		if (Progress >= List[Index].Target)
		{
			var done = List[Index];
			Index++;
			Progress = 0;

			ShowToast($"KÜLDETÉS KÉSZ: {done.Title}  +{done.Aura} AURA", Yellow);
			_player.AddAura(done.Aura);
		}

		Refresh();
	}

	private void Refresh()
	{
		if (Index >= List.Length)
		{
			_title.Text = "Minden küldetés kész! 👑";
			_progress.Text = "";
			return;
		}

		var quest = List[Index];
		_title.Text = quest.Title;
		_progress.Text = quest.Target > 1 ? $"{Number(Progress)} / {Number(quest.Target)}" : "";
	}

	private void ShowToast(string text, Color color)
	{
		_toast.Text = text;
		_toast.AddThemeColorOverride("font_color", color);

		_toastTween?.Kill();
		_toast.Modulate = White;
		_toastTween = CreateTween();
		_toastTween.TweenInterval(2.5f);
		_toastTween.TweenProperty(_toast, "modulate:a", 0.0f, 0.5f);
	}

	private static string Number(int n) => n.ToString("#,0").Replace(",", " ");

	private void BuildUi()
	{
		var root = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.AddThemeConstantOverride("margin_top", 12);
		root.AddThemeConstantOverride("margin_right", 12);
		AddChild(root);

		var panel = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
			CustomMinimumSize = new Vector2(260, 0),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		var frame = new StyleBoxFlat
		{
			BgColor = new Color(0, 0, 0, 0.7f),
			BorderColor = new Color(1, 1, 1, 0.5f),
			ContentMarginLeft = 12,
			ContentMarginRight = 12,
			ContentMarginTop = 8,
			ContentMarginBottom = 8,
		};
		frame.SetBorderWidthAll(2);
		frame.SetCornerRadiusAll(4);
		panel.AddThemeStyleboxOverride("panel", frame);
		root.AddChild(panel);

		var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		box.AddThemeConstantOverride("separation", 2);
		panel.AddChild(box);

		Label heading = NewLabel(box, 12, Yellow);
		heading.Text = "KÜLDETÉS";

		_title = NewLabel(box, 16, White);
		_title.AutowrapMode = TextServer.AutowrapMode.Word;
		_title.CustomMinimumSize = new Vector2(236, 0);

		_progress = NewLabel(box, 13, new Color(1, 1, 1, 0.6f));

		_toast = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Modulate = new Color(1, 1, 1, 0),
		};
		_toast.SetAnchorsPreset(Control.LayoutPreset.TopWide);
		_toast.OffsetTop = 90;
		_toast.AddThemeFontSizeOverride("font_size", 24);
		_toast.AddThemeColorOverride("font_outline_color", Colors.Black);
		_toast.AddThemeConstantOverride("outline_size", 8);
		AddChild(_toast);
	}

	private static Label NewLabel(Control parent, int size, Color color)
	{
		var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		parent.AddChild(label);
		return label;
	}
}
