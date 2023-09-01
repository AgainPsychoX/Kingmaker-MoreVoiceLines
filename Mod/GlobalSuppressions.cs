// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", 
    Justification = "Method to load the mod, called via reflection by Unity Mod Manager.", 
    Scope = "member", Target = "~M:MoreVoiceLines.MoreVoiceLines.Load(UnityModManagerNet.UnityModManager.ModEntry)~System.Boolean")]
[assembly: SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", 
    Justification = "Patch.", 
    Scope = "member", Target = "~M:MoreVoiceLines.VoiceOverStatusPatches.Stop.Prefix~System.Boolean")]
[assembly: SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", 
    Justification = "Patch.", 
    Scope = "member", Target = "~M:MoreVoiceLines.LocalizedStringPatches.PlayVoiceOver.Prefix(Kingmaker.Localization.LocalizedString@,Kingmaker.Localization.VoiceOverStatus@,UnityEngine.MonoBehaviour)~System.Boolean")]
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", 
    Justification = "Patch; argument needs to be specified to determine right method.", 
    Scope = "member", Target = "~M:MoreVoiceLines.LocalizedStringPatches.PlayVoiceOver.Prefix(Kingmaker.Localization.LocalizedString@,Kingmaker.Localization.VoiceOverStatus@,UnityEngine.MonoBehaviour)~System.Boolean")]
