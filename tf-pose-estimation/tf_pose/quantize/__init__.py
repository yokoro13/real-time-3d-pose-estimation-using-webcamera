"""Graph rewriting for quantization-aware training.

Ported from tensorflow/contrib/quantize (TensorFlow r1.15), which was removed in TensorFlow 2.x.
"""

from tf_pose.quantize.quantize_graph import create_eval_graph, create_training_graph
