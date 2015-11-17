using UnityEngine;
using System.Collections;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour {

    public GameObject player;
    public GameObject audioGameObject;

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
        for(int i = 0; i < audioGameObject.transform.childCount; i++)
        {
            float dist = Vector3.Distance(audioGameObject.transform.GetChild(i).position, player.transform.position);
            float defVolume = 1 / dist * 20; // 20 * Mathf.Log10(dist);
            audioGameObject.transform.GetChild(i).GetComponent<AudioSource>().outputAudioMixerGroup.audioMixer.SetFloat("volume", defVolume);
        }
	}
}
