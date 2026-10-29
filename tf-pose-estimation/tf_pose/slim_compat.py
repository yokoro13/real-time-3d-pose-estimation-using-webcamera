import tensorflow as tf
from tensorflow.python.framework import ops

# tf_slim is no longer maintained and still refers to ops.Tensor, which was removed in recent TensorFlow releases.
if not hasattr(ops, 'Tensor'):
    ops.Tensor = tf.Tensor

import tf_slim as slim
