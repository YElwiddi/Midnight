EXTERNAL SetEventVar(varName, value)

VAR player_friendly = 0
VAR player_mean = 0
VAR player_scared = 0
VAR player_smart = 0
VAR player_brave = 0
VAR player_stupid = 0

=== start ===
#speaker: Harold Vunderbilt
I suppose some problems do solve themselves.

+ [I thought you two were close.]
    -> harold_grave
+ [What did you say your name was again?]
    -> harold_grave_2

=== harold_grave ===
Just saying goodbye properly.

I'll be leaving now.
Thanks for looking the other way.
-> END


=== harold_grave_2 ===
Harold. Harold Vanderbilt.
I'll be taking my leave now. Have a great rest of your evening.
-> END