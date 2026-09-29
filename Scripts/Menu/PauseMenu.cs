using Godot;

// Esc: szünet menü (Folytatás / Főmenü / Kilépés), nyitva áll a játék.
// A nyitott párbeszéd és kaszinó az Esc-et az _Input-ban lenyeli, így ide csak akkor ér, ha más nincs nyitva.
// ponytail: a UI kódból épül, mint a Dialogue - egy CanvasLayer node a World-ben.
public partial class PauseMenu : CanvasLayer
{
	private static readonly Color Yellow = new Color(1, 1, 0);
	private static readonly Color White = new Color(1, 1, 1);

	private Control _root;
	private Button _first;

	public override void _Ready()
	{
		Layer = 30;
		ProcessMode = ProcessModeEnum.Always;

		BuildUi();
		_root.Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel") || CasinoGame.IsOpen)
			return;

		SetOpen(!_root.Visible);
		GetViewport().SetInputAsHandled();
	}

	private void SetOpen(bool open)
	{
		_root.Visible = open;
		GetTree().Paused = open;

		if (open)
			_first.GrabFocus();
	}

	// Ugyanaz, mint az ablak bezárása: a félbehagyott kör lezárul (rendőrök), a SaveGame ment.
	private void Leave()
	{
		GetTree().Root.PropagateNotification((int)NotificationWMCloseRequest);
		GetTree().Paused = false;
	}

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

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(320, 0) };
		var frame = new StyleBoxFlat
		{
			BgColor = new Color(0, 0, 0, 0.9f),
			BorderColor = White,
			ContentMarginLeft = 20,
			ContentMarginRight = 20,
			ContentMarginTop = 16,
			ContentMarginBottom = 16,
		};
		frame.SetBorderWidthAll(3);
		frame.SetCornerRadiusAll(5);
		panel.AddThemeStyleboxOverride("panel", frame);
		center.AddChild(panel);

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 8);
		panel.AddChild(box);

		var title = new Label { Text = "SZÜNET", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeFontSizeOverride("font_size", 28);
		title.AddThemeColorOverride("font_color", Yellow);
		box.AddChild(title);

		_first = AddButton(box, "Folytatás", () => SetOpen(false));

		AddButton(box, "Mentés és főmenü", () =>
		{
			Leave();
			GetTree().ChangeSceneToFile("res://Scenes/Menu/TitleScreen.tscn");
		});

		AddButton(box, "Mentés és kilépés", () =>
		{
			Leave();
			GetTree().Quit();
		});
	}

	private static Button AddButton(Control parent, string text, System.Action pressed)
	{
		var button = new Button { Text = text };

		button.AddThemeFontSizeOverride("font_size", 20);
		button.AddThemeColorOverride("font_color", White);
		button.AddThemeColorOverride("font_hover_color", Colors.Black);
		button.AddThemeColorOverride("font_focus_color", Colors.Black);
		button.AddThemeColorOverride("font_pressed_color", Colors.Black);

		foreach (string state in new[] { "normal", "hover", "focus", "pressed" })
		{
			var style = new StyleBoxFlat
			{
				BgColor = state == "normal" ? new Color(1, 1, 1, 0.05f) : Yellow,
				ContentMarginTop = 6,
				ContentMarginBottom = 6,
			};
			style.SetCornerRadiusAll(3);
			button.AddThemeStyleboxOverride(state, style);
		}

		button.MouseEntered += () => button.GrabFocus();
		button.Pressed += () => pressed();
		parent.AddChild(button);
		return button;
	}
}
