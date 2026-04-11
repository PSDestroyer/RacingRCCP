using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TrackPanel : MonoBehaviour
{
    private List<MapSelect.TrackData> tracks = new List<MapSelect.TrackData>();
    private MapSelect mapSelect;
    public List<Image> tracksBUttons = new List<Image>(4);

    public void ShowTracks(MapSelect owner, List<MapSelect.TrackData> trackList)
    {
        mapSelect = owner;
        tracks = trackList ?? new List<MapSelect.TrackData>();
        Initialize();
    }

    public void Initialize()
    {
        for (int i = 0; i < tracksBUttons.Count; i++)
        {
            bool hasTrack = i < tracks.Count;
            tracksBUttons[i].gameObject.SetActive(hasTrack);

            Button button = tracksBUttons[i].GetComponent<Button>();

            if (button != null)
                button.onClick.RemoveAllListeners();

            if (!hasTrack)
                continue;

            tracksBUttons[i].sprite = tracks[i].trackImage;

            if (button != null)
            {
                int capturedIndex = i;
                button.onClick.AddListener(() => SelectTrack(capturedIndex));
            }
        }
    }

    public void SelectTrack(int index)
    {
        if (mapSelect == null)
            return;

        if (index < 0 || index >= tracks.Count)
            return;

        mapSelect.SelectTrack(index);
    }
}
