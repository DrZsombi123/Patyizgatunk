using Godot;

// A fodrászat belső tere: belépéskor zene, Ferike mellett E = hajvágás.
public partial class BarberShop : Area2D
{
	[Export]
	public Area2D FerikeArea { get; set; }

	[Export]
	public float HairDuration { get; set; } = 120.0f;

	[Export]
	public int AuraBonus { get; set; } = 10;

	private AudioStreamPlayer _music;
	private Player _customer;

	public override void _Ready()
	{
		_music = GetNode<AudioStreamPlayer>("Music");

		BodyEntered += body => { if (body is Player) _music.Play(); };
		BodyExited += body => { if (body is Player) _music.Stop(); };

		FerikeArea.BodyEntered += body => { if (body is Player player) _customer = player; };
		FerikeArea.BodyExited += body => { if (body == _customer) _customer = null; };
	}

	public override void _Process(double delta)
	{
		if (_customer != null && Input.IsActionJustPressed("pickup"))
			_customer.GetHaircut(HairDuration, AuraBonus);
	}
}
