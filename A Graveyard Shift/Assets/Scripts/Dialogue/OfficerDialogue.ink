EXTERNAL SetEventVar(varName, value)
EXTERNAL SuspendDialogue()

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR GraveyardProtection = 100
VAR Sanity = 100

=== start ===
Evening.
My name is officer James. I'm here with my partner, officer Baidey on an investigation.
We've heard reports of some disturbances coming from both from this cemetary and the surrounding neighborhood.
Apparently some folks saw some strange gang behavior. We took the call, and that's why we're here.
Would you mind answering some questions for us?

+ [Alright, how can I help you?]
~player_scared = player_scared + 1
    -> interrogation_accept
+ [Sorry, I can't help you.]
    -> interrogation_decline

=== interrogation_accept ===
Great, so we've received reports of a few men breaking and entering into homes around this neighborhood.
Apparently, some of them have been sighted near this cemetary as well.
I'd like your help in conducting this investigation.
Firstly, have you seen anyone attempting to enter the premises dressed in black?

+ [No, officer.]
    -> next_question
+ [Yes, I have.]
    -> interrogate_entry
    
=== interrogate_entry ===
Could you describe the behavior of this individiual?

+ [They asked to come in and see a grave.]
~GraveyardProtection = GraveyardProtection + 5
    -> next_question
+ [They never approached me.]
    -> next_question
+ [I don't remember.]
    -> next_question


=== next_question ===
Alright, I'll move on to my next question.
Have you seen anyone stalking the perimeter of this facility? Either by the walls, or the gate, looking in?

+ [Yes.]
    -> describe_stalker
+ [No.]
    -> ask_for_entry

=== describe_stalker ===
Do you remember anything about this individual?
Perhaps the color of his clothes or complexion?

+ [They wearing a greenish brown shirt.]
~GraveyardProtection = GraveyardProtection + 10

    -> ask_for_entry
+ [They were wearing a grey robe.]
~Sanity = Sanity - 10
    -> ask_for_entry
    
+ [I don't remember.]
    -> ask_for_entry
    
    === ask_for_entry ===
Alright, we appreciate your coopearation.
We have one more question.
If it's alright with you...
-> ask

=== ask ===
I would like to come inside and take a look around. 
+ [Alright, go ahead.]
~GraveRobberSetup = GraveRobberSetup - 1
~SpiritAngered = SpiritAngered + 1
~ SetEventVar("allowed_inside", true)
-> END
+ [I'm afraid I can't let you in sir.]
    -> decline
~ SetEventVar("allowed_inside", false)
+ [What did you say your name was again?]
    -> ask_name_again
 + [Hold on, I'll be right back.]
      ~ SuspendDialogue()
    -> ask
    
=== ask_name_again ===
My name is officer James.
-> ask


=== interrogation_decline ===
I see...

-> ask


=== decline===
~ SetEventVar("allowed_inside", false)
Well, we don't have a warrant, so I can't force you to let me in.
We'll be nearby if there's any trouble. Keep an eye out.
-> END