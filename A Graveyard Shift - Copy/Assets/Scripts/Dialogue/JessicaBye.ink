EXTERNAL SetEventVar(varName, value)

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0

=== start ===
Oh.
Um.
I think I'm okay now.
I stayed longer than I meant to.
It feels quieter in here.
Not good quiet.
Just... tired quiet.
I should probably go home.

+ [You sure you’re alright to leave alone?]
    -> leaving_alone



=== leaving_alone ===
Y-yeah.
I mean.
I walked here by myself.
And nothing bad happened.
Probably.
So it should be fine going back.
If I walk fast.
But not run.
Running makes it obvious you're scared.

~player_stupid = player_stupid + 1

+ [Goodnight.]
    -> goodbye

=== goodbye ===
Okay.
Thank you.
For letting me in.
And for not laughing.
I'll be gone in just a second.

-> END