using Godot;

// Minecraft-stílusú csík a képernyő alján: 6 rekesz, a kijelölt sárga kerettel.
// 1-6 vagy egérgörgő vált, Q használja a kijelöltet (a hatást a Player kapja).
// ponytail: nincs ikon az Art-ban, ezért a rekeszben a tárgy neve áll.
// ponytail: a UI kódból épül, mint a Dialogue - egy CanvasLayer node a World-ben.
public partial class Hotbar : CanvasLayer
{
	public const int Slots = 6;

	public static Hotbar Current { get; private set; }

	private static readonly Color Yellow = new Color(1, 1, 0);

	private readonly string[] _items = new string[Slots];
	private readonly int[] _counts = new int[Slots];
	private readonly PanelContainer[] _panels = new PanelContainer[Slots];
	private readonly TextureRect[] _icons = new TextureRect[Slots];
	private readonly Label[] _labels = new Label[Slots];

	private Control _root;
	private Player _player;
	private int _selected = 0;

	public override void _Ready()
	{
		Current = this;
		Layer = 9;

		_player = GetNode<Player>("../Player");

		BuildUi();
		Refresh();
	}

	public override void _ExitTree()
	{
		if (Current == this)
			Current = null;
	}

	// Ugyanaz a tárgy egy rekeszbe gyűlik, különben az első üresbe kerül.
	// Tele a csík: false, hogy a hívó tudja, nem fért bele.
	public bool Add(string item)
	{
		for (int i = 0; i < Slots; i++)
		{
			if (_items[i] == item)
			{
				_counts[i]++;
				Refresh();
				return true;
			}
		}

		for (int i = 0; i < Slots; i++)
		{
			if (_items[i] == null)
			{
				_items[i] = item;
				_counts[i] = 1;
				_selected = i;
				Refresh();
				return true;
			}
		}

		return false;
	}

	public override void _Process(double delta)
	{
		// párbeszéd közben a panel eltakarná a csíkot
		_root.Visible = !Dialogue.IsOpen;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (Dialogue.IsOpen)
			return;

		if (@event is InputEventKey key && key.Pressed && !key.Echo)
		{
			int index = (int)key.Keycode - (int)Key.Key1;

			if (index >= 0 && index < Slots)
			{
				Select(index);
				GetViewport().SetInputAsHandled();
			}
			else if (key.Keycode == Key.Q)
			{
				UseSelected();
				GetViewport().SetInputAsHandled();
			}

			return;
		}

		if (@event is InputEventMouseButton wheel && wheel.Pressed)
		{
			if (wheel.ButtonIndex == MouseButton.WheelDown)
				Select((_selected + 1) % Slots);
			else if (wheel.ButtonIndex == MouseButton.WheelUp)
				Select((_selected + Slots - 1) % Slots);
		}
	}

	private void Select(int index)
	{
		_selected = index;
		Refresh();
	}

	private void UseSelected()
	{
		string item = _items[_selected];

		if (item == null || !_player.Use(item))
			return;

		if (--_counts[_selected] <= 0)
			_items[_selected] = null;

		Refresh();
	}

	private void Refresh()
	{
		for (int i = 0; i < Slots; i++)
		{
			_panels[i].AddThemeStyleboxOverride("panel", Slot(i == _selected));

			Texture2D icon = Icon(_items[i]);
			_icons[i].Texture = icon;

			// ikonos tárgynál csak a darabszám kell a sarokba, egyébként a név középen
			_labels[i].HorizontalAlignment = icon != null ? HorizontalAlignment.Right : HorizontalAlignment.Center;
			_labels[i].VerticalAlignment = icon != null ? VerticalAlignment.Bottom : VerticalAlignment.Center;

			_labels[i].Text =
				_items[i] == null ? ""
				: icon != null ? (_counts[i] > 1 ? $"x{_counts[i]}" : "")
				: _counts[i] > 1 ? $"{_items[i]}\nx{_counts[i]}" : _items[i];
		}
	}

	// ponytail: a fájl a tárgy nevéből jön, nincs hozzá tábla. Nincs kép -> marad a szöveg.
	private static Texture2D Icon(string item)
	{
		if (item == null)
			return null;

		string path = $"res://Art/item_{item}.png";
		return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
	}

	private void BuildUi()
	{
		_root = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddThemeConstantOverride("margin_bottom", 12);
		AddChild(_root);

		var column = new VBoxContainer
		{
			Alignment = BoxContainer.AlignmentMode.End,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		_root.AddChild(column);

		var row = new HBoxContainer
		{
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};

		row.AddThemeConstantOverride("separation", 4);
		column.AddChild(row);

		for (int i = 0; i < Slots; i++)
		{
			var panel = new PanelContainer
			{
				CustomMinimumSize = new Vector2(54, 54),
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};

			row.AddChild(panel);

			// a PanelContainer egymásra teríti a gyerekeit: ikon alul, felirat fölötte
			var icon = new TextureRect
			{
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepCentered,
			};

			panel.AddChild(icon);

			var label = new Label
			{
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.Word,
			};

			label.AddThemeFontSizeOverride("font_size", 12);
			panel.AddChild(label);

			_panels[i] = panel;
			_icons[i] = icon;
			_labels[i] = label;
		}
	}

	private static StyleBoxFlat Slot(bool selected)
	{
		var box = new StyleBoxFlat
		{
			BgColor = new Color(0, 0, 0, 0.7f),
			BorderColor = selected ? Yellow : new Color(1, 1, 1, 0.5f),
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
			ContentMarginLeft = 4,
			ContentMarginRight = 4,
		};

		box.SetBorderWidthAll(selected ? 3 : 2);
		return box;
	}
}
