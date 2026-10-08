extends SceneTree

# Automata füstteszt: végigmegy a főbb funkciókon, és képernyőképeket ment a docs/img mappába.
# Futtatás (ablakkal, mert a képernyőképhez renderelni kell), a projekt gyökeréből:
#   Godot_v4.7.2-stable_mono_win64_console.exe --path . -s res://Tests/SmokeTest.gd
# A kilépési kód a hibás tesztek száma. A meglévő mentést félreteszi, a végén visszarakja.

const SAVE := "user://save.cfg"
const BACKUP := "user://save.cfg.bak"
const SHOTS := "res://docs/img/"

var _failed := 0
var _passed := 0


func _initialize() -> void:
	_run()


func _run() -> void:
	_backup_save()
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(SHOTS))

	# --- Menük ---
	await _load("res://Scenes/Menu/TitleScreen.tscn")
	var buttons := _texts(current_scene, "Button")
	_check("TC-04", "Főmenü betölt, vannak gombjai", buttons.size() >= 3, ", ".join(buttons))
	await _shot("fomenu")

	await _load("res://Scenes/Menu/Options.tscn")
	_check("TC-05", "Beállítások menü betölt", _texts(current_scene, "Label").size() > 0, "")
	await _shot("beallitasok")

	await _load("res://Scenes/Menu/Credits.tscn")
	var credits := " ".join(_texts(current_scene, "RichTextLabel"))
	_check("TC-06", "Alkotók: mindhárom csapattag szerepel",
		credits.contains("Molnár János") and credits.contains("Girán Zsombor") and credits.contains("Sándor Gerg"), credits.strip_edges())
	await _shot("alkotok")

	# --- Játék ---
	await _load("res://Scenes/World.tscn")
	var world := current_scene
	var player := world.get_node("Player")
	var quests := world.get_node("Quests")
	var hotbar := world.get_node("Hotbar")
	_check("TC-07", "Új játék kezdőállapota (50 000 Ft, 0 aura, 1. küldetés)",
		player.Money == 50000 and player.Aura == 0 and quests.Index == 0,
		"pénz=%d aura=%d küldetés=%d" % [player.Money, player.Aura, quests.Index])
	await _shot("jatek")

	var start_x: float = player.global_position.x
	Input.action_press("move_right")
	await create_timer(0.5).timeout
	Input.action_release("move_right")
	_check("TC-08", "Mozgás: D lenyomva jobbra megy a karakter", player.global_position.x > start_x + 10,
		"x: %.0f -> %.0f" % [start_x, player.global_position.x])

	var brendon := _find_npc(world, "kulcs")
	var options := _effects(brendon.Available(player))
	_check("TC-09", "Brendon: kulcs nélkül a kocsikulcs kérhető, a pótkulcs nem",
		options.has("kulcs") and not options.has("potkulcs"), ", ".join(options))

	var key_option := ""
	for option in brendon.Options:
		if option.split("|")[1] == "kulcs":
			key_option = option
	brendon.Choose(key_option, player)
	await _frames(2)
	_check("TC-10", "Kocsikulcs elkérése: kulcs a zsebben, a küldetés teljesül",
		player.HasCarKey and Array(hotbar.Items).has("kulcs") and quests.Index == 1 and not _effects(brendon.Available(player)).has("kulcs"),
		"kulcs=%s küldetés=%d" % [player.HasCarKey, quests.Index])

	player.HasCarKey = false
	player.KeySeized = true
	options = _effects(brendon.Available(player))
	_check("TC-11", "Lefoglalt kulcs után Brendon pótkulcsot ad (a sima kulcs nem kérhető)",
		options.has("potkulcs") and not options.has("kulcs"), ", ".join(options))
	player.HasCarKey = true

	var items: PackedStringArray = hotbar.Items
	var counts: PackedInt32Array = hotbar.Counts
	var added := 0
	while hotbar.Add("teszt%d" % added) and added < 10:
		added += 1
	_check("TC-12", "Tele zseb: 6 rekesz után nem fér több tárgy", added == 5, "%d új tárgy fért be a kulcs mellé" % added)
	hotbar.Restore(items, counts)

	await _tap("phone")
	await create_timer(0.6).timeout
	var phone_camera := _first(world.get_node("Phone"), "SubViewport") as SubViewport
	_check("TC-13", "Telefon: F1-re feljön", phone_camera.render_target_update_mode == SubViewport.UPDATE_ALWAYS, "")
	await _shot("telefon")
	await _tap("phone")
	await create_timer(0.5).timeout

	var night: CanvasModulate = world.get_node("Night")
	await _tap("night")
	await create_timer(2.0).timeout
	_check("TC-14", "Éjszaka: N-re besötétedik", night.color.b < 0.6 and night.color.r < 0.5, str(night.color))

	var car := world.get_node("Merci")
	var lights: Node2D = car.get_node("Lights")
	var lights_parked := lights.visible
	player.global_position = car.ExitPosition
	await _frames(10)
	await _tap("vehicle")
	await _frames(5)
	_check("TC-15", "Beszállás (F) kulccsal; éjjel csak vezetés közben ég a lámpa",
		player.InCar and lights.visible and not lights_parked, "kocsiban=%s lámpa=%s" % [player.InCar, lights.visible])
	await _shot("ejszaka_auto")
	await _tap("vehicle")
	await _frames(5)
	_check("TC-16", "Kiszállás (F)", not player.InCar, "")
	await _tap("night")

	world.get_node("CasinoGame").Open("roulette", player)
	await create_timer(0.5).timeout
	await _shot("kaszino")
	await _tap("ui_cancel")
	await _frames(5)
	paused = false

	await _tap("ui_cancel")
	_check("TC-17", "Esc: szünet menü, a játék megáll", paused, "")
	await _shot("szunet")
	await _tap("ui_cancel")
	_check("TC-18", "Esc újra: a játék folytatódik", not paused, "")

	var drunk := _first(player, "", "DrunkSystem")
	drunk.AddDrunk(60.0)
	await create_timer(1.0).timeout
	_check("TC-19", "Részegség: 60%-nál zavart irányítás fokozat", drunk.Drunkness > 50.0, drunk.GetStatusName())
	await _shot("reszeg")

	# --- Mentés / betöltés ---
	player.Money = 12345
	world.get_node("SaveGame").notification(Node.NOTIFICATION_WM_CLOSE_REQUEST)
	var cfg := ConfigFile.new()
	var loaded := cfg.load(SAVE) == OK
	_check("TC-20", "Mentés: a save.cfg-be bekerül a pénz és a lefoglalt kulcs",
		loaded and cfg.get_value("player", "money") == 12345 and cfg.get_value("player", "key_seized") == true, "")

	await _load("res://Scenes/World.tscn")
	world = current_scene
	player = world.get_node("Player")
	_check("TC-21", "Betöltés: újraindítás után megvan a pénz, a kulcs és a küldetés",
		player.Money == 12345 and player.HasCarKey and player.KeySeized and world.get_node("Quests").Index == 1,
		"pénz=%d küldetés=%d" % [player.Money, world.get_node("Quests").Index])

	player.AddAura(player.WinAura)
	await create_timer(1.0).timeout
	_check("TC-22", "Győzelem: 500 aurától győzelmi képernyő, a mentés törlődik",
		current_scene.scene_file_path.ends_with("Victory.tscn") and not FileAccess.file_exists(SAVE), current_scene.name)
	await _shot("gyozelem")

	_restore_save()
	print("\nEREDMÉNY: %d sikeres, %d sikertelen" % [_passed, _failed])
	quit(_failed)


func _check(id: String, title: String, ok: bool, detail: String) -> void:
	if ok:
		_passed += 1
	else:
		_failed += 1
	print("%s  %s  %s  %s" % [id, "SIKERES" if ok else "HIBÁS", title, detail])


func _load(path: String) -> void:
	paused = false
	change_scene_to_file(path)
	await create_timer(1.5).timeout


func _frames(count: int) -> void:
	for i in count:
		await process_frame


func _tap(action: String) -> void:
	for pressed in [true, false]:
		var event := InputEventAction.new()
		event.action = action
		event.pressed = pressed
		Input.parse_input_event(event)
		await _frames(3)


func _shot(file: String) -> void:
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(ProjectSettings.globalize_path(SHOTS + file + ".png"))


# az összes adott típusú node szövege (gombok, feliratok)
func _texts(node: Node, type: String) -> PackedStringArray:
	var result := PackedStringArray()
	for child in node.find_children("*", type, true, false):
		if child.text.strip_edges() != "":
			result.append(child.text.strip_edges())
	return result


# az első node a típus vagy a script fájlneve alapján
func _first(node: Node, type: String, script := "") -> Node:
	for child in node.find_children("*", type, true, false):
		if script == "" or (child.get_script() and child.get_script().resource_path.ends_with(script + ".cs")):
			return child
	return null


# az az NPC, akinek van ilyen hatású válaszlehetősége
func _find_npc(node: Node, effect: String) -> Node:
	for child in node.find_children("*", "", true, false):
		if child.get_script() and child.get_script().resource_path.ends_with("Npc.cs"):
			if _effects(child.Options).has(effect):
				return child
	return null


func _effects(options: PackedStringArray) -> PackedStringArray:
	var result := PackedStringArray()
	for option in options:
		if option.split("|")[1] != "":
			result.append(option.split("|")[1])
	return result


func _backup_save() -> void:
	if FileAccess.file_exists(SAVE):
		DirAccess.rename_absolute(ProjectSettings.globalize_path(SAVE), ProjectSettings.globalize_path(BACKUP))


func _restore_save() -> void:
	DirAccess.remove_absolute(ProjectSettings.globalize_path(SAVE))
	if FileAccess.file_exists(BACKUP):
		DirAccess.rename_absolute(ProjectSettings.globalize_path(BACKUP), ProjectSettings.globalize_path(SAVE))
