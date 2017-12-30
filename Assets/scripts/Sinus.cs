using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sinus : MonoBehaviour {

    public double frequency = 440;
    public double gain = 0.05;

    private double increment;
    private double phase;
    private double sampling_frequency = 48000;



    // Use this for initialization
    void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    void OnAudioFilterRead(float[] data, int channels)
    {
        increment = frequency * 2 * System.Math.PI / sampling_frequency;
        for (var i = 0; i < data.Length; i = i + channels)
        {
            phase = phase + increment;
            // this is where we copy audio data to make them “available” to Unity
            data[i] = (float)(gain * System.Math.Sin(phase));
            // if we have stereo, we copy the mono data to each channel
            if (channels == 2) data[i + 1] = data[i];
            if (phase > 2 * System.Math.PI) phase = 0;
        }
    }
}
