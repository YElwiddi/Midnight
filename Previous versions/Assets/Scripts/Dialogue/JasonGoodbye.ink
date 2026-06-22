EXTERNAL SetEventVar(varName, value)

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0

=== start ===
…Hey.
I'm done.

Took longer than I thought.
Guess I had more to say than I realized.

+ [Everything alright?]
    -> alright_response
+ [You should be heading out.]
~player_mean = player_mean + 1
    -> hurry_response

=== alright_response ===
Yeah.
As alright as it gets, I suppose.

Edward's stone is still clean.
Someone's been taking care of it.

Anyway.
Thanks for letting me in.

-> farewell_response

=== hurry_response ===
Right.
Yeah.
You're right.

Don't worry.
I wasn't planning on staying.
They've had enough company for one night.

-> farewell_response


=== farewell_response ===
...
Try not to take this job too personally.
People just come here to mourn.

Good night.

 ~ SetEventVar("allowed_inside", false)
-> END
