EXTERNAL SetEventVar(varName, value)
EXTERNAL SuspendDialogue()

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0

=== start ===
H-hey...
I need to come in to visit a grave...
Is that okay?

+ [Who are you?]
    -> who_are_you_response
+ [Well, go on ahead in!]
    ~player_stupid = player_stupid + 2
    -> let_in_response
    
=== who_are_you_response ===
You mean, like, my name?
It's, uh...
Edwin.
Can I go in now?

+ [I'll need your full name.]
    -> full_name_response
+ [Sure.]
    ~player_stupid = player_stupid + 1
    -> let_in_response

=== full_name_response ===
M-my full name?
My name is Edwin Adams.
I'm here to visit my mother's grave.
I don't have any identification with me, I didn't think anyone would be here.
I don't live far from here and I've been here before a few times. There was never anyone here at the gate.

-> ask_for_entry

=== ask_for_entry ===
Can I please go in now?

+ [Alright. Go ahead.]
    -> let_in_response
+ [Sorry kid. Graveyard's closed.]
    ~player_mean = player_mean + 2
    -> deny_response
+ [What did you say your name was again?]
    -> ask_name_again
  + [Hold on, I'll be right back.]
      ~ SuspendDialogue()
    -> ask_for_entry
    
=== ask_name_again ===
I-it's Edwin Adams.

-> ask_for_entry

=== let_in_response ===
T-thanks...
I won't be long...
 ~ SetEventVar("allowed_inside", true)
-> END

=== deny_response ===
...
 ~ SetEventVar("allowed_inside", false)
 ~ SpiritAngered = SpiritAngered + 1

-> END
