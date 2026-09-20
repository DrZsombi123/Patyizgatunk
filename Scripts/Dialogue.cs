using Godot;

// Felugró párbeszédablak: az NPC neve, egy mondata és a választható válaszok.
// Válaszolni egérrel, nyilak + Enterrel vagy az 1-9 számokkal lehet, Esc bezár.
// A kinézet a főmenüt követi: fekete panel fehér kerettel, sárga kijelölés.
// ponytail: a UI kódból épül, nincs hozzá külön .tscn - egy CanvasLayer node a World-ben.
public partial class Dialogue : CanvasLayer
{
	public static Dialogue Current { get; private set; }

	public static bool IsOpen => Current != null && Current._root.Visible;

	private static readonly Color Yellow = new Color(1, 1, 0);
	private static readonly Color Black = new Color(0, 0, 0);
	private static readonly Color White = new Color(1, 1, 1);

	private Control _root;
	private Label _name;
	private Label _money;
	private Label _text;
	private VBoxContainer _choices;
	private Tween _typing;
	private Npc _npc;
	private Player _player;
	private int _optionCount = 0;

	public override void _Ready()
	{
		Current = this;
		Layer = 10;

		BuildUi();
		_root.Visible = false;
	}

	public override void _ExitTree()
	{
		if (Current == this)
			Current = null;
	}

	public void Open(Npc npc, Player player)
	{
		_npc = npc;
		_player = player;

		_root.Visible = true;
		_name.Text = npc.DisplayName;

		BuildChoices();
		Say(npc.Greeting());
	}

	public void Close()
	{
		_root.Visible = false;
		_npc = null;
	}

	public override void _Input(InputEvent @event)
	{
		if (!IsOpen)
			return;

		if (@event.IsActionPressed("ui_cancel"))
		{
			Close();
			GetViewport().SetInputAsHandled();
			return;
		}

		// 1-9: a sokadik válasz
		if (@event is InputEventKey key && key.Pressed && !key.Echo)
		{
			int index = (int)key.Keycode - (int)Key.Key1;

			if (index >= 0 && index < _optionCount)
			{
				Choose(index);
				GetViewport().SetInputAsHandled();
			}
		}
	}

	private void Choose(int index)
	{
		Say(_npc.Choose(index, _player));
		ShowMoney();
	}

	private void ShowMoney()
	{
		_money.Text = _player.Money.ToString("#,0").Replace(",", " ") + " Ft";
	}

	// a szöveg betűnként fut ki, hogy ne csak odacsapódjon
	private void Say(string line)
	{
		_typing?.Kill();

		_text.Text = line;
		_text.VisibleRatio = 0.0f;

		_typing = CreateTween();
		_typing.TweenProperty(_text, "visible_ratio", 1.0, Mathf.Clamp(line.Length * 0.016f, 0.2f, 1.1f));
	}

	private void BuildUi()
	{
		_root = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddThemeConstantOverride("margin_left", 24);
		_root.AddThemeConstantOverride("margin_right", 24);
		_root.AddThemeConstantOverride("margin_bottom", 40);
		AddChild(_root);

		// az ablak a képernyő aljára tapad, középre igazítva
		var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		_root.AddChild(column);

		var panel = new PanelContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			CustomMinimumSize = new Vector2(720, 0),
		};

		panel.AddThemeStyleboxOverride("panel", Frame());
		column.AddChild(panel);

		var inner = new MarginContainer();
		foreach (string side in new[] { "left", "right", "top", "bottom" })
			inner.AddThemeConstantOverride("margin_" + side, 14);
		panel.AddChild(inner);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		inner.AddChild(box);

		var header = new HBoxContainer();
		box.AddChild(header);

		_name = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		_name.AddThemeFontSizeOverride("font_size", 21);
		_name.AddThemeColorOverride("font_color", Yellow);
		header.AddChild(_name);

		_money = new Label { VerticalAlignment = VerticalAlignment.Center };
		_money.AddThemeFontSizeOverride("font_size", 15);
		_money.AddThemeColorOverride("font_color", new Color(1, 1, 1, 0.55f));
		header.AddChild(_money);

		box.AddChild(new ColorRect
		{
			Color = new Color(1, 1, 1, 0.25f),
			CustomMinimumSize = new Vector2(0, 1),
		});

		// fix magasság, hogy ne ugráljon a panel a hosszabb válaszoktól
		_text = new Label
		{
			AutowrapMode = TextServer.AutowrapMode.Word,
			CustomMinimumSize = new Vector2(0, 46),
		};

		_text.AddThemeFontSizeOverride("font_size", 18);
		box.AddChild(_text);

		_choices = new VBoxContainer();
		_choices.AddThemeConstantOverride("separation", 2);
		box.AddChild(_choices);
	}

	private void BuildChoices()
	{
		foreach (Node child in _choices.GetChildren())
		{
			_choices.RemoveChild(child);
			child.QueueFree();
		}

		string[] labels = _npc.OptionLabels();
		_optionCount = labels.Length;
		ShowMoney();

		for (int i = 0; i < labels.Length; i++)
		{
			int index = i;
			Button button = NewChoice($"{i + 1}.  {labels[i]}");
			button.Pressed += () => Choose(index);
		}

		Button leave = NewChoice("Hagyjuk.");
		leave.Pressed += Close;

		_choices.GetChild<Control>(0).GrabFocus();
	}

	private Button NewChoice(string label)
	{
		var button = new Button
		{
			Text = label,
			Alignment = HorizontalAlignment.Left,
		};

		button.AddThemeFontSizeOverride("font_size", 17);
		button.AddThemeColorOverride("font_color", White);
		button.AddThemeColorOverride("font_hover_color", Black);
		button.AddThemeColorOverride("font_focus_color", Black);
		button.AddThemeColorOverride("font_pressed_color", Black);
		button.AddThemeStyleboxOverride("normal", Row(new Color(1, 1, 1, 0.05f)));
		button.AddThemeStyleboxOverride("hover", Row(Yellow));
		button.AddThemeStyleboxOverride("focus", Row(Yellow));
		button.AddThemeStyleboxOverride("pressed", Row(Yellow));

		// az egér alatt lévő sor legyen a kijelölt is, hogy egér és nyilak ne vesszenek össze
		button.MouseEntered += () => button.GrabFocus();

		_choices.AddChild(button);
		return button;
	}

	private static StyleBoxFlat Row(Color color)
	{
		var row = new StyleBoxFlat
		{
			BgColor = color,
			ContentMarginLeft = 12,
			ContentMarginRight = 12,
			ContentMarginTop = 4,
			ContentMarginBottom = 4,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
		};

		return row;
	}

	private static StyleBoxFlat Frame()
	{
		var frame = new StyleBoxFlat
		{
			BgColor = new Color(0, 0, 0, 0.85f),
			BorderColor = White,
			BorderBlend = true,
			CornerRadiusTopLeft = 5,
			CornerRadiusTopRight = 5,
			CornerRadiusBottomLeft = 5,
			CornerRadiusBottomRight = 5,
			ShadowSize = 10,
			ShadowColor = new Color(0, 0, 0, 0.5f),
		};

		frame.SetBorderWidthAll(3);
		return frame;
	}
}
