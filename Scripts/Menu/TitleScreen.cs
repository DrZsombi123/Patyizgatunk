using Godot;

// A főmenü újratöltődik, ha a Beállításokból / Alkotókból visszajövünk.
// Hogy a bevezető videó és a zene ne induljon elölről, a pozíciójuk itt átvészeli a jelenetváltást.
public partial class TitleScreen : Control
{
	private static double _videoPosition = 0.0;
	private static float _audioPosition = 0.0f;

	private VideoStreamPlayer _video;
	private AudioStreamPlayer _audio;

	public override void _Ready()
	{
		_video = GetNode<VideoStreamPlayer>("VideoStreamPlayer");
		_audio = GetNode<AudioStreamPlayer>("AudioStreamPlayer");

		// első indulás: az autoplay elölről kezdi, nincs mit folytatni
		if (_videoPosition <= 0.0)
			return;

		_video.StreamPosition = _videoPosition;
		_audio.Play(_audioPosition);
	}

	// nem _ExitTree: addigra a lejátszók már leálltak és 0-t adnának
	public override void _Process(double delta)
	{
		_videoPosition = _video.StreamPosition;
		_audioPosition = _audio.GetPlaybackPosition();
	}
}
