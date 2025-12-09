using UnityEngine;

public class RadioController : MonoBehaviour
{
    [Header("Assign your AudioSource here")]
    public AudioSource radioAudio;  
    private bool isOn = false;
    
    
    public void OnRadioToggleChanged(bool isOn)
    {
        Debug.Log(isOn);
        if (isOn)
        {
            TurnOn();
        }
        else
        {
            TurnOff();
        }
    }

    private void TurnOn()
    {
        if (!isOn)
        {
            radioAudio.Play();   // Plays the assigned AudioClip
            isOn = true;
            Debug.Log("Radio is ON");
        }
    }

    private void TurnOff()
    {
        if (isOn)
        {
            radioAudio.Pause();  // Pauses the clip
            isOn = false;
            Debug.Log("Radio is OFF");
        }
    }
}