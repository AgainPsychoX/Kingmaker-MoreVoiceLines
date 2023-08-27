
^+s::
	While GetKeyState("Ctrl") or GetKeyState("Shift") or GetKeyState("s")
    {
        Sleep 100
    }
	Send, {Alt}
	Sleep, 100
	Send, {Enter}
	Sleep, 100
	Send, e
	Sleep, 100
	Send, w
	Sleep, 100
	Send, {Enter}
	WinWait, Eksportuj dźwięk
	Sleep, 100
	Send, {Enter}
	Sleep, 100
	WinWait, Ostrzeżenie
	Sleep, 100
	Send, {Enter}
	Sleep, 100
	Send, !{F4}
	Sleep, 100
	Send, n
	return

^+!m::
	Send, {Ctrl Down}c{Ctrl Up}
	Sleep, 50
	Send, {Left}
	Sleep, 25
	Send, {Backspace}{Backspace}
	Sleep, 25
	Send, {Ctrl Down}
	Sleep, 50
	Send, "``"
	Sleep, 50
	Send {Ctrl Up}
	Sleep, 50
	Send, {Ctrl Down}c{Ctrl Up}
	Sleep, 50
	Send, & "C:\Program Files\Audacity\Audacity.exe"{Space}
	Sleep, 50
	Send, {Ctrl Down}v{Ctrl Up}
	Sleep, 50
	Send, {Enter}
	return
