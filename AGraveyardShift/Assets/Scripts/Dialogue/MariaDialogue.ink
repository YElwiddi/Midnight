EXTERNAL SetEventVar(varName, value)
EXTERNAL SuspendDialogue()

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR Sanity = 100
VAR GraveyardProtection = 100

// =====================================================================
//  MARIA - gate dialogue (played at NPCGatePoint)
//  Structure mirrors the other visitors (Chen / Priest).
//  Fill in the real lines; keep the allow/deny knots calling SetEventVar
//  so the waypoint branch on "allowed_inside" still works.
// =====================================================================

=== start ===
#speaker: ???
...
-> maria_opening

=== maria_opening ===
// TODO: Maria's opening beat. Two intro choices, like the other visitors.
+ [Are you here to visit someone?]
    -> maria_visit
+ [It's late to be out here.]
    // ~ player_mean = player_mean + 1
    -> maria_visit

=== maria_visit ===
#speaker: Maria
...
+ [Who are you visiting?]
    -> who_visit
+ [Are you alright?]
    -> who_visit

=== who_visit ===
...
...
...Let me in.
Please...
-> maria_request

=== maria_request ===
Just let me in.
+ [Okay, come in.]
    -> maria_allow
+ [I'm sorry ma'am, please go home.]
    -> maria_deny
+ [I have a few more questions.]
    -> questions
+ [Hold on, I'll be right back.]
      ~ SuspendDialogue()
    -> maria_request
    
=== questions ===
+ [Who are you visiting?]
    -> who_visit_q
+ [What is your name?]
    -> name
+ [I have no more questions.]
    -> maria_request
    
=== who_visit_q ===
...
...
-> questions

=== name ===
...
Maria.
-> questions
    
    
=== maria_allow ===
#speaker: Maria
...
// ~ Sanity = Sanity - 0   // optional: sanity cost for letting her in
~ SetEventVar("allowed_inside", true)
-> END

=== maria_deny ===
#speaker: Maria
...
~ SetEventVar("allowed_inside", false)
-> END
