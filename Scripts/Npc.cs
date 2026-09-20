using Godot;

// Beszélhető NPC. A player InteractArea-ja a szülő testet érzékeli, E -> következő szöveg.
// A szöveg a testvér "Label"-ben jelenik meg, ShowTime múlva visszaáll a név.
// ponytail: nincs külön dialógus-UI, az NPC névtáblája a buborék.
public partial class Npc : Node2D
{
	[Export]
	public string[] Lines { get; set; } = System.Array.Empty<string>();

	[Export]
	public string Item { get; set; } = "";     // "patyi" vagy "finlandia", üres = csak duma

	[Export]
	public int Price { get; set; } = 0;        // > 0 -> minden megszólítás vásárlás

	[Export]
	public float ShowTime { get; set; } = 3.5f;

	private Label _label;
	private string _name = "";
	private Timer _timer;
	private int _next = 0;

	public override void _Ready()
	{
		_label = GetParent().GetNodeOrNull<Label>("Label");

		if (_label != null)
		{
			_name = _label.Text;
			_label.AutowrapMode = TextServer.AutowrapMode.Word;
		}

		_timer = new Timer { OneShot = true };
		AddChild(_timer);
		_timer.Timeout += () => { if (_label != null) _label.Text = _name; };
	}

	public void Interact(Player player)
	{
		if (Price > 0)
			Buy(player);
		else if (Lines.Length > 0)
			Say(Lines[_next++ % Lines.Length], player);
	}

	private void Buy(Player player)
	{
		if (player.Money < Price)
		{
			Say($"Nincs annyi lóvéd. {Price} Ft a tarifa.", player);
			return;
		}

		player.Money -= Price;

		// ponytail: két cucc van, ezért switch. Ha több lesz, exportált hatásértékek.
		if (Item == "patyi")
			player.TakePatyi();
		else
			player.DrinkFinlandia();

		Say(Lines.Length > 0 ? Lines[_next++ % Lines.Length] : "Tessék.", player);
	}

	private void Say(string line, Player player)
	{
		if (_label == null)
			return;

		_label.Text = line.Replace("{penz}", player.Money.ToString());
		_timer.Start(ShowTime);
	}
}
