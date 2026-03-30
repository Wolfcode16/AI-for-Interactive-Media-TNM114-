# AI-for-Interactive-Media-TNM114
Project where I implemented a self learning car to to navigate in a dynamic race track via PPO.

## About
This project evaluates the efficiency of Proximal Policy Optimization (PPO) in developing autonomous navigation
capabilities within a physics-based Unity simulation. By employing the ML-Agents framework, a car agent was
tasked with navigating a closed-loop track featuring dynamic and randomized obstacles. The study specifically
investigates the transition from sparse to dense reward structures to overcome the credit assignment problem
and mitigate ”lazy” agent behaviors, such as intentional self-termination. Results show that a combination of
hierarchical reward shaping by balancing survival incentives with checkpoint milestones, and curriculum based
obstacle randomization, significantly improves policy generalization. The final model successfully transitioned
from memorizing static paths to reactive obstacle avoidance, achieving near-optimal lap completion rates across
varied obstacle configurations.

## Tech Stack
- C#
- Unity
- Unity ML-Agents
