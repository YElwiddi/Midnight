EXTERNAL SetEventVar(varName, value)

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0

=== start ===
It's been a long time since I've been to this grave site.
To be honest with you...
It's been a long time since I've been outside at all.
I haven't left home in a long time. Just can't find a reason for it. For anything at all.

+ [Are you going to be okay?]
    -> are_you_okay
+ [Get lost, kid.]
    ~player_mean = player_mean + 2
    ~SpiritAngered = SpiritAngered + 1
    -> leave
    
=== are_you_okay ===
Yeah... I'll get through this.
Thanks for letting me in, I know it's pretty late and you have a job to do.
But this helps more than you know.
    -> END


=== leave ===
...

    -> END