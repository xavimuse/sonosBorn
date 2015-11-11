/************************************************************************/
/* Copyright (C) 2011-2015 Impulsonic Inc. All Rights Reserved.         */
/*                                                                      */
/* The source code, information  and  material ("Material") contained   */
/* herein is owned  by Impulsonic Inc. or its suppliers or licensors,   */
/* and title to such  Material remains  with Impulsonic  Inc.  or its   */
/* suppliers or licensors. The Material contains proprietary informa-   */
/* tion  of  Impulsonic or  its  suppliers and licensors. No  part of   */
/* the Material may be used, copied, reproduced, modified, published,   */
/* uploaded, posted, transmitted, distributed or disclosed in any way   */
/* without Impulsonic's prior express written permission. No  license   */
/* under  any patent, copyright or other intellectual property rights   */
/* in the Material is  granted  to  or  conferred  upon  you,  either   */
/* expressly, by implication, inducement, estoppel or otherwise.  Any   */
/* license  under  such intellectual property rights must  be express   */
/* and approved by Impulsonic in writing.                               */
/*                                                                      */
/* Third Party trademarks are the property of their respective owners.  */
/*                                                                      */
/* Unless otherwise  agreed upon by Impulsonic  in  writing, you  may   */
/* not remove or  alter this  notice or any other  notice embedded in   */
/* Materials by Impulsonic or Impulsonic's  suppliers or licensors in   */
/* any way.                                                             */
/************************************************************************/

using System;
using System.Collections.Generic;
using System.IO;

using UnityEngine;

using Phonon;


//
// Phonon3DListener
// Represents a binaural listener and its HRTF.
//

[AddComponentMenu("Phonon/Phonon 3D Listener")]
public class Phonon3DListener : MonoBehaviour
{
	//
	// Initializes the listener.
	//
	void Awake()
	{
		if (effectEnabled)
			return;

		if (AudioEngineComponent.GetAudioEngine() == AudioEngine.Unity)
		{
			// Save out the audio pipeline settings.
			int numBuffers;
			AudioSettings.GetDSPBufferSize(out frameSize, out numBuffers);
			
			// Initialize the audio pipeline.
			if (Phonon3D.iplInitializeAudioPipeline(AudioSettings.outputSampleRate, frameSize, SpeakerLayout.STEREO) != Error.NONE)
			{
				Debug.Log("Unable to initialize Phonon audio pipeline. Please check the log file for details.");
				return;
			}
		}

		// Construct the full path to the HRTF file.
#if UNITY_ANDROID && !UNITY_EDITOR
		string hrtfAssetFile = Path.Combine(Application.streamingAssetsPath, hrtfFileName);
		Debug.Log(hrtfAssetFile);
		WWW streamingAssetLoader = new WWW(hrtfAssetFile);
		while (!streamingAssetLoader.isDone) ;
		byte[] assetData = streamingAssetLoader.bytes;
		string hrtfPath = Path.Combine(Application.temporaryCachePath, hrtfFileName);
		BinaryWriter dataWriter = new BinaryWriter(new FileStream(hrtfPath, FileMode.Create));
		dataWriter.Write(assetData);
#else
		string hrtfPath = Path.Combine(Application.streamingAssetsPath, hrtfFileName);
#endif
		
		// Copy the listener settings.
		ListenerSettings listenerSettings;
		listenerSettings.maxSources = MaxSources;
		listenerSettings.maxDistance = MaxDistance;
		listenerSettings.minAttenuation = 0.02f;

		// Create the listener.
		if (AudioEngineComponent.GetAudioEngine() == AudioEngine.Unity)
		{
			// Create the listener.
			if (Phonon3D.iplCreateListener(hrtfPath, listenerSettings) != Error.NONE)
			{
				Debug.Log("Unable to create Phonon 3D Listener. Please check the log file for details.");
				return;
			}
		}
		else if (AudioEngineComponent.GetAudioEngine() == AudioEngine.Unity5)
		{
			int numBuffers;
			AudioSettings.GetDSPBufferSize(out frameSize, out numBuffers);
			Phonon3DUnity5.iplUnity5InitializePhonon3D(AudioSettings.outputSampleRate, frameSize, hrtfPath, listenerSettings);
		}
		else if (AudioEngineComponent.GetAudioEngine() == AudioEngine.FMODStudio)
		{
			// Create the listener.
			Phonon3DFMOD.iplFMODInitialize(hrtfPath, listenerSettings);
		}
        else if (AudioEngineComponent.GetAudioEngine() == AudioEngine.Wwise)
        {
            Phonon3DWwise.iplWwiseCreateListener(hrtfPath, listenerSettings);
        }

		audioEngine = AudioEngineComponent.GetAudioEngine();

		effectEnabled = true;
		
		if (AudioEngineComponent.GetAudioEngine() == AudioEngine.Unity)
		{
			// Create any queued sources.
			if (sourceQueue != null)
			{
				foreach (Phonon3DSource source in sourceQueue)
					source.InitializePhonon3DSource();
				sourceQueue.Clear();
			}
		}
	}
	
	//
	// Destroys the listener.
	//
	void OnDestroy()
	{
		if (!effectEnabled)
			return;
		
		effectEnabled = false;
		
		if (audioEngine == AudioEngine.Unity)
		{
			// Disable all sources.
			Phonon3DSource[] sources = GameObject.FindObjectsOfType<Phonon3DSource>();
			foreach (Phonon3DSource source in sources)
			{
				source.SetEffectEnabled(false);
			}
			
			// Destroy all sources.
			foreach (Phonon3DSource source in sources)
			{
				while (source.IsEffectProcessing());
				Phonon3D.iplDestroySource(source.GetSource());
			}
			
			// Destroy the listener.
			Phonon3D.iplDestroyListener();
		}
	}
	
	//
	// Updates the listener position and orientation.
	//
	void Update()
	{
		if (!effectEnabled)
			return;
		
		if (audioEngine == AudioEngine.Unity)
		{
			Phonon3D.iplUpdateListener(Common.ConvertVector(transform.position), Common.ConvertVector(transform.forward), Common.ConvertVector(transform.up));
		}
		else if (audioEngine == AudioEngine.Unity5)
		{
			Phonon3DUnity5.iplUnity5UpdateListener();
		}
		else if (audioEngine == AudioEngine.FMODStudio)
		{
			Phonon3DFMOD.iplFMODUpdateListener();
		}
	}
	
	//
	// Returns the frame size.
	//
	public static int GetFrameSize()
	{
		return frameSize;
	}
	
	public static bool IsEffectEnabled()
	{
		return effectEnabled;
	}
	
	public static void EnqueueSource(Phonon3DSource source)
	{
		if (sourceQueue == null)
			sourceQueue = new Queue<Phonon3DSource>();
		
		sourceQueue.Enqueue(source);
	}
	
	//
	// Data members.
	//
	
	// Is the effect enabled?
	static bool effectEnabled = false;
	
	// Default HRTF file name.
	static string hrtfFileName = "cipic_124.hrtf";
	
	// Audio pipeline settings.
	static int frameSize = 0;
	
	// List of sources enqueued for initialization.
	static Queue<Phonon3DSource> sourceQueue = null;
	
	// Audio Engine.
	Phonon.AudioEngine audioEngine;
	
	//
	// Public properties.
	//
	
	[Range(1, 64)]
	public int MaxSources = 32;
	
	[Range(0.0f, 500.0f)]
	public float MaxDistance = 100.0f;
}