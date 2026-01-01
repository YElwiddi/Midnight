EXTERNAL SetEventVar(varName, value)

VAR player_friendly = 0
VAR player_mean = 0
VAR player_scared = 0
VAR player_smart = 0
VAR player_brave = 0
VAR player_stupid = 0

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
    ~player_brave = player_brave + 1
    -> brave_response
+ [Okay. Take your time and tell me what you need.]
    ~player_friendly = player_friendly + 1
    -> friendly_response

=== brave_response ===
#speaker: Mr. Huang
Late is better.
Less people. Less noise.
My wife not like noise.

I stand outside and think.
That all.

+ [Which grave are you visiting?]
    ~player_smart = player_smart + 1
    -> wife_response
+ [You can understand why this looks suspicious.]
    ~player_mean = player_mean + 1
    -> suspicious_response

=== friendly_response ===
#speaker: Mr. Huang
Thank you.
English hard for me.
My wife help me before.

She here.
Long time already.

+ [I'm sorry for your loss.]
    ~player_friendly = player_friendly + 1
    -> sympathy_response
+ [You shouldn’t be waiting outside the gate.]
    ~player_stupid = player_stupid + 1
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
Section C.
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
May I go inside?
Just few minutes.
I visit my wife.
Then I leave.

+ [Alright. You can go in, but don’t stay long.]
    ~player_friendly = player_friendly + 1
    -> allow_entry
+ [I’m sorry. Visiting hours are over.]
    ~player_mean = player_mean + 1
    -> deny_entry

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
 ~ SetEventVar("allowed_inside", false)

-> END
