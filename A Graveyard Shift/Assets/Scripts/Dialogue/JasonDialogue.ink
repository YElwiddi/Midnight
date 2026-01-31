EXTERNAL SetEventVar(varName, value)

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR Sanity = 100

=== start ===
Evening.
Gate still open, huh?
Good.
I need to get inside for a minute.
Just here to visit some graves.

+ [Who are you?]
    -> who_are_you_response
+ [Go on in.]
    ~player_stupid = player_stupid + 2
    -> let_in_response

=== who_are_you_response ===
Who am I?
Right.
Name's Jason.
Jason Holmes.

Didn't expect anyone to be standing guard tonight.
Usually it's quiet out here.
Real quiet.

+ [I'll need your full name.]
    -> full_name_response
+ [Alright. Go ahead.]
    ~player_stupid = player_stupid + 1
    -> let_in_response

=== full_name_response ===
Jason Michael Holmes.
I'm here for my brother, Zackary Holmes.
And his son. Edward Holmes.

They were buried together, at the same area, on the same day.
I won't be long.
Just need to say what I never got to.

Can I go in?

+ [Alright. Go ahead.]
    -> let_in_response
+ [Sorry. Graveyard's closed.]
    ~player_mean = player_mean + 2
    -> deny_response


=== let_in_response ===
Thanks.
I appreciate it.

 ~ SetEventVar("allowed_inside", true)
-> END

=== deny_response ===
…
Yeah.
I should've expected that.

They never did like being left alone.
Funny how some habits stick.

 ~ SetEventVar("allowed_inside", false)
 ~ SpiritAngered = SpiritAngered + 1
 ~ Sanity = Sanity - 20

-> END
