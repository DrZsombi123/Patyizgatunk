using Godot;

// Mentés a user://save.cfg-be: hely, pénz, aura, kulcs, hotbar, telefon, küldetések, a merci helye.
// A World legalján él: mire a _Ready-je fut, a többi node már kész, így rögtön betölthetünk.
// 30 mp-enként és kilépéskor ment. Győzelemkor a Victory törli, a főmenüben "Új játék" is.
// ponytail: a részegség, a szerhatások és a séró nem mentődik - betöltés után józanon, rossz séróval
// indulunk. Ha kell, a DrunkSystem.Drunkness is ide jöhet.
public partial class SaveGame : Node
{
	private const string FilePath = "user://save.cfg";

	public static bool Exists => FileAccess.FileExists(FilePath);

	public static void Delete()
	{
		if (Exists)
			DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(FilePath));
	}

	private Player _player;
	private Car _car;
	private Phone _phone;

	public override void _Ready()
	{
		_player = GetNode<Player>("../Player");
		_car = GetNode<Car>("../Merci");
		_phone = GetNode<Phone>("../Phone");

		var timer = new Timer { WaitTime = 30.0, Autostart = true };
		AddChild(timer);
		timer.Timeout += Save;

		Load();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest)
			Save();
	}

	private void Save()
	{
		// kiütés közben épp teleportálunk, majd a következő kör elmenti
		if (_player.IsBlackedOut)
			return;

		var cfg = new ConfigFile();

		// kocsiban ülve a kiszállás helyére mentünk, különben a kocsiba töltenénk be
		cfg.SetValue("player", "position", _player.InCar ? _car.ExitPosition : _player.GlobalPosition);
		cfg.SetValue("player", "money", _player.Money);
		cfg.SetValue("player", "aura", _player.SavedAura);
		cfg.SetValue("player", "key", _player.HasCarKey);

		cfg.SetValue("car", "position", _car.GlobalPosition);
		cfg.SetValue("car", "rotation", _car.Rotation);

		cfg.SetValue("hotbar", "items", System.Array.ConvertAll(Hotbar.Current.Items, item => item ?? ""));
		cfg.SetValue("hotbar", "counts", Hotbar.Current.Counts);

		cfg.SetValue("phone", "followers", _phone.Followers);
		cfg.SetValue("phone", "roses", _phone.Roses);

		cfg.SetValue("quests", "index", Quests.Current.Index);
		cfg.SetValue("quests", "progress", Quests.Current.Progress);

		Error error = cfg.Save(FilePath);
		if (error != Error.Ok)
			GD.PushError($"SaveGame: nem sikerült menteni ({error}).");
	}

	private void Load()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(FilePath) != Error.Ok)
			return;

		_player.GlobalPosition = (Vector2)cfg.GetValue("player", "position", _player.GlobalPosition);
		_player.Money = (int)cfg.GetValue("player", "money", _player.Money);
		_player.RestoreAura((int)cfg.GetValue("player", "aura", 0));
		_player.HasCarKey = (bool)cfg.GetValue("player", "key", false);

		_car.GlobalPosition = (Vector2)cfg.GetValue("car", "position", _car.GlobalPosition);
		_car.Rotation = (float)cfg.GetValue("car", "rotation", _car.Rotation);

		Hotbar.Current.Restore(
			(string[])cfg.GetValue("hotbar", "items", System.Array.Empty<string>()),
			(int[])cfg.GetValue("hotbar", "counts", System.Array.Empty<int>()));

		_phone.Followers = (int)cfg.GetValue("phone", "followers", _phone.Followers);
		_phone.Roses = (int)cfg.GetValue("phone", "roses", 0);

		Quests.Current.Restore((int)cfg.GetValue("quests", "index", 0), (int)cfg.GetValue("quests", "progress", 0));
	}
}
