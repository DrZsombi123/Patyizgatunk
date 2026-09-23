using Godot;

// Beszélhető NPC. A player InteractArea-ja a szülő testet érzékeli, E -> felugró ablak.
// A név a testvér "Label"-ből jön, a szövegek a Lines-ból körbe.
// Egy válaszlehetőség formátuma: "felirat|hatás|ár|válasz".
// hatás: patyi, pia, energia, hajvagas, kulcs vagy üres (csak duma).
public partial class Npc : Node2D
{
	[Export]
	public string[] Lines { get; set; } = System.Array.Empty<string>();

	[Export]
	public string[] Options { get; set; } = System.Array.Empty<string>();

	[Export]
	public AudioStreamPlayer Sound { get; set; }   // sikeres vásárlásra szól, ha van

	private string _name = "";
	private int _next = 0;

	public override void _Ready()
	{
		Label label = GetParent().GetNodeOrNull<Label>("Label");
		_name = label != null ? label.Text : GetParent().Name;
	}

	public string DisplayName => _name;

	public string Greeting()
	{
		return Lines.Length > 0 ? Lines[_next++ % Lines.Length] : "...";
	}

	// A most választható opciók: ami már nem értelmes (kulcs, ha nálunk van), nem jelenik meg.
	public string[] Available(Player player)
	{
		return System.Array.FindAll(Options, option => !(player.HasCarKey && Effect(option) == "kulcs"));
	}

	public static string Label(string option) => option.Split('|')[0];

	private static string Effect(string option)
	{
		string[] parts = option.Split('|');
		return parts.Length > 1 ? parts[1] : "";
	}

	public string Choose(string option, Player player)
	{
		string[] parts = option.Split('|');

		string effect = Effect(option);
		int price = parts.Length > 2 ? parts[2].ToInt() : 0;
		string reply = parts.Length > 3 ? parts[3] : "Aha.";

		if (player.Money < price)
			return $"Nincs meg a {price} Ft. Gyere vissza, ha összejött.";

		// ponytail: pár hatás van, ezért switch. Ha sok lesz, exportált értékek jönnek.
		// A szerek a hotbarba kerülnek, onnan lehet elsütni őket (Q).
		switch (effect)
		{
			case "patyi":
			case "jack":
			case "finlandia":
			case "energia":
			case "kulcs":
				// tele a csík: nem vesszük el a pénzt sem
				if (!Hotbar.Current.Add(effect))
					return "Tele a zsebed, Márió. Használj el előbb valamit.";

				if (effect == "kulcs")
					player.HasCarKey = true;

				break;

			case "hajvagas": player.GetHaircut(120.0f, 10); break;
		}

		player.Money -= price;

		if (effect != "" && Sound != null)
			Sound.Play();

		return reply.Replace("{penz}", player.Money.ToString());
	}
}
