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

	public string[] OptionLabels()
	{
		var labels = new string[Options.Length];

		for (int i = 0; i < Options.Length; i++)
			labels[i] = Options[i].Split('|')[0];

		return labels;
	}

	public string Choose(int index, Player player)
	{
		string[] parts = Options[index].Split('|');

		string effect = parts.Length > 1 ? parts[1] : "";
		int price = parts.Length > 2 ? parts[2].ToInt() : 0;
		string reply = parts.Length > 3 ? parts[3] : "Aha.";

		if (effect == "kulcs" && player.HasCarKey)
			return "Nálad van a kulcs, Márió. Ne veszítsd el.";

		if (player.Money < price)
			return $"Nincs meg a {price} Ft. Gyere vissza, ha összejött.";

		player.Money -= price;

		// ponytail: pár hatás van, ezért switch. Ha sok lesz, exportált értékek jönnek.
		switch (effect)
		{
			case "patyi": player.TakePatyi(); break;
			case "pia": player.DrinkPia(); break;
			case "energia": player.DrinkEnergy(); break;
			case "hajvagas": player.GetHaircut(120.0f, 10); break;
			case "kulcs": player.HasCarKey = true; break;
		}

		if (effect != "" && Sound != null)
			Sound.Play();

		return reply.Replace("{penz}", player.Money.ToString());
	}
}
