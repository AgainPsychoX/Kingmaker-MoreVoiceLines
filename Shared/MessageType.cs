
namespace MoreVoiceLines.IPC
{
    public enum MessageType
    {
        /// <summary>
        /// Special value to indicate message type is unknown. 
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// If returned to message handling, means the other side disconnected.
        /// </summary>
        Disconnected,

        /// <summary>
        /// No operation, no parameters, returns nothing.
        /// </summary>
        None,

        /// <summary>
        /// Requests player to stop the server pipe, clean up and exit.
        /// </summary>
        Exit,

        /// <summary>
        /// Requests message being echo-ed. Parameters: 
        /// + 16 bit unsigned integer as length of the message to be replied;
        /// + and the message itself.
        /// Both the length of the message and the message are replied, see `EchoResponse`.
        /// </summary>
        EchoRequest,
        /// <summary>
        /// Response message for echo request, the same structure as `EchoRequest`.
        /// </summary>
        EchoResponse,

        /// <summary>
        /// Notifies player that the settings was updated, and it should read the `Settings.xml` file again.
        /// Returns nothing.
        /// </summary>
        SettingsUpdated,

        /// <summary>
        /// Starts playing audio from specified path. Parameters: binary encoded string for path.
        /// Returns nothing.
        /// </summary>
        PlayAudio,

        /// <summary>
        /// Stops audio playback (effectively also recipe), no parameters.
        /// Returns nothing.
        /// </summary>
        StopAudio,

        /// <summary>
        /// Notifies client that the audio piece finished playing, unless was stopped.
        /// Also fires for each audio in recipe. No data is transfered.
        /// </summary>
        FinishedAudio,

        /// <summary>
        /// Plays recipe by UUID as 36 characters string, like '1a65e653-b705-4107-afbf-a7bafa63f190'.
        /// Returns nothing.
        /// </summary>
        PlayRecipe,

        /// <summary>
        /// Stops recipe playback.
        /// Returns nothing.
        /// </summary>
        StopRecipe,

        /// <summary>
        /// Notifies client that the recipe finished playing, unless was stopped.
        /// No data is transfered.
        /// </summary>
        FinishedRecipe,
    }
}
