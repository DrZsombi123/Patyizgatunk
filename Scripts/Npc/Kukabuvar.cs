using Godot;

// A kuka mellett elhaladva kiugrik a kukabúvár, ordít egyet, majd visszabújik.
// Cooldown után újra élesedik, hogy ne minden elsétálásnál ijesztgessen.
public partial class Kukabuvar : Area2D
{
	[Export]
	public string[] Shouts { get; set; } = { "BOO!", "BÚÚÚÚ!", "HÁT TE MEG?!" };

	[Export]
	public float ShowTime { get; set; } = 2.0f;

	[Export]
	public float Cooldown { get; set; } = 12.0f;

	private Node2D _guy;
	private Label _label;
	private Timer _hide;
	private Timer _reload;
	private bool _armed = true;
	private int _next = 0;

	public override void _Ready()
	{
		_guy = GetNode<Node2D>("Guy");
		_label = GetNode<Label>("Label");

		_guy.Visible = false;
		_label.Visible = false;

		_hide = NewTimer(() =>
		{
			_guy.Visible = false;
			_label.Visible = false;
			_reload.Start(Cooldown);
		});

		_reload = NewTimer(() => _armed = true);

		BodyEntered += body => { if (body is Player player) JumpOut(player); };
	}

	private void JumpOut(Player player)
	{
		if (!_armed)
			return;

		_armed = false;

		_label.Text = Shouts[_next++ % Shouts.Length];
		_guy.Visible = true;
		_label.Visible = true;

		// kipattan a kuka mögül: alulról fel
		Vector2 target = _guy.Position;
		_guy.Position = target + new Vector2(0, 26);

		CreateTween()
			.TweenProperty(_guy, "position", target, 0.25)
			.SetTrans(Tween.TransitionType.Back)
			.SetEase(Tween.EaseType.Out);

		player.Scare();
		_hide.Start(ShowTime);
	}

	private Timer NewTimer(System.Action onTimeout)
	{
		var timer = new Timer { OneShot = true };
		AddChild(timer);
		timer.Timeout += onTimeout;
		return timer;
	}
}
