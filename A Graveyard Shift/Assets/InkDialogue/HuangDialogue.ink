EXTERNAL SetEventVar(varName, value)
EXTERNAL SuspendDialogue()

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0

=== start ===
#speaker: Mr. Huang
Hello. Excuse me.
This is graveyard, yes?
I come to visit my wife.

-> id_check

=== id_check ===
+ [Can I see some identification?]
    -> id_response

=== id_response ===
#speaker: Mr. Huang
Identification?
Yes… yes.
My name Mr. Huang.
I am him.
I not carry many papers now.
Only me. Only husband.

I come many times before.
Maybe you not see me.
I come late when quiet.

+ [Alright. Why are you here so late?]
    -> brave_response
+ [Okay. Take your time and tell me what you need.]
    ~player_mean = player_mean - 1
    -> friendly_response

=== brave_response ===
#speaker: Mr. Huang
Late is better.
Less people. Less noise.
My wife not like noise.

I stand outside and think.
That all.

+ [Which grave are you visiting?]
    ~player_stupid = player_stupid - 1
    -> wife_response
+ [You can understand why this looks suspicious.]
    ~player_stupid = player_stupid + 1
    -> suspicious_response

=== friendly_response ===
#speaker: Mr. Huang
Thank you.
English hard for me.
My wife help me before.

She here.
Long time already.

+ [I'm sorry for your loss.]
    -> sympathy_response
+ [You shouldn’t be waiting outside the gate.]
    ~player_stupid = player_stupid + 1
    ~player_mean = player_mean + 1
    -> suspicious_response

=== suspicious_response ===
#speaker: Mr. Huang
I know.
I look bad.
Old man. Night time.

But I not do wrong.
I promise.
I only want see her.

-> converge_request

=== wife_response ===
#speaker: Mr. Huang
Western burial ground.
Near tree.
She like tree.

I talk to her sometimes.
It help me.

-> converge_request

=== sympathy_response ===
#speaker: Mr. Huang
Thank you.
House very quiet now.
Here feel closer to her.

I not stay long.

-> converge_request

=== converge_request ===
#speaker: Mr. Huang
Please.
Just few minutes.
I visit my wife.
Then I leave.

-> ask

===ask===
May I come in?

+ [Alright. You can go in, but don’t stay long.]
    -> allow_entry
+ [I’m sorry. Visiting hours are over.]
    ~player_mean = player_mean + 1
    -> deny_entry
+ [I have a few more questions.]
    -> more_questions
+ [Hold on, I'll be right back.]
      ~ SuspendDialogue()
    -> ask
    
=== more_questions ===
+ [What was your name again?]
    -> ask_name_again
+ [Who are you visiting?]
    -> who_visit
+ [How many people are in your family?]
    -> how_many
+ [Nevermind.]
-> ask

=== who_visit ===
My late wife.
Mei Huang.
+ [When did she die?]
-> when
+ [What was her cause of death?]
-> cause_of_death
+ [I have other questions.]
-> more_questions
+ [Nevermind.]
-> ask

===when===
I do not remember...
It has been long time.
+ [What was her cause of death?]
-> cause_of_death
+ [I have other questions.]
-> more_questions
+ [I have no other questions.]
-> ask
===cause_of_death===
I do not like to speak about this matter.
My wife not bad person.
She did no wrong.
Please, I would like to see her.
+ [When did she die?]
-> when
+ [I have other questions.]
-> more_questions
+ [I have no other questions.]
-> ask
=== how_many===
No one.
Only me and wife. After passing away, I am alone.
+ [I have more questions.]
-> more_questions
+ [I have no other questions.]
-> ask
=== ask_name_again ===
My name?
Huang Wenqi.
-> more_questions

=== allow_entry ===
#speaker: Mr. Huang
Thank you.
You very kind.
 ~ SetEventVar("allowed_inside", true)
-> END

=== deny_entry ===
#speaker: Mr. Huang
I understand.
Rules are rules.

I come again tomorrow.
Thank you for listening.
    ~ player_stupid = player_stupid + 2
 ~ SetEventVar("allowed_inside", false)
 ~ SpiritAngered = SpiritAngered + 1

-> END
